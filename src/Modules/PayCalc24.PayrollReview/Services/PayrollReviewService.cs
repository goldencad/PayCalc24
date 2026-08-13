using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollFunds;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.PayrollReview;
using System.Globalization;

namespace PayCalc24.PayrollReview.Services;

/// <summary>Read-only projections over immutable, explicitly selected result datasets.</summary>
public sealed class PayrollReviewService(IPayrollReviewSource source) : IPayrollReviewService
{
    public PayrollValidationSummary GetPayrollValidationSummary(ReviewResultContext context)
    {
        var dataset = Load(context);
        var findings = Findings(dataset);
        var errors = findings.Count(x => x.Severity == DiagnosticSeverity.Error);
        var warnings = findings.Count(x => x.Severity == DiagnosticSeverity.Warning);
        var infos = findings.Count(x => x.Severity == DiagnosticSeverity.Info);
        var blocking = findings.Any(x => x.IsBlocking);
        return new(context.CompanyId, context.PayrollPeriodId, context.SnapshotId, context.CalculationRunId,
            errors, warnings, infos, blocking, !blocking,
            errors > 0 ? ReviewStatus.HasErrors : warnings > 0 ? ReviewStatus.HasWarnings : ReviewStatus.Clean,
            findings);
    }

    public IReadOnlyList<ValidationFinding> ListValidationFindings(ReviewResultContext context) => Findings(Load(context));

    public IReadOnlyList<ValidationFinding> GetSubjectValidation(ReviewResultContext context, PayrollSubjectId subjectId) =>
        Findings(Load(context)).Where(x => x.PayrollSubjectId == subjectId).ToArray();

    public PayrollExplainResult ExplainPayrollSubject(ReviewResultContext context, PayrollSubjectId subjectId)
    {
        var data = Load(context);
        var run = RequireRun(data);
        var subject = data.Snapshot.HistoricalFacts.Subjects.SingleOrDefault(x => x.PayrollSubjectId == subjectId)
            ?? throw Error(DiagnosticCodes.PayrollReviewExplainProvenanceIncomplete, ("subjectId", subjectId.Value));
        var subjectResult = data.SubjectResults.SingleOrDefault(x => x.PayrollSubjectId == subjectId)
            ?? throw Error(DiagnosticCodes.PayrollReviewExplainProvenanceIncomplete, ("subjectId", subjectId.Value));
        var components = data.ComponentResults.Where(x => x.PayrollSubjectId == subjectId)
            .OrderBy(x => x.Sequence).ThenBy(x => x.ComponentCode, StringComparer.Ordinal)
            .Select(x => BuildComponent(data, x)).ToArray();
        var funds = data.FundResults.Where(x => x.Members.Any(m => m.PayrollSubjectId == subjectId))
            .OrderBy(x => FundVersion(data, x).Code, StringComparer.Ordinal).Select(x => BuildFund(data, x)).ToArray();
        var scheme = data.Snapshot.PolicyConfiguration.CompensationVersions
            .SingleOrDefault(x => x.CompensationSchemeId == subject.CompensationSchemeId);
        var children = new List<ExplainNode>
        {
            new("HISTORICAL_FACTS", subject.EmployeeCode, Children: data.Snapshot.HistoricalFacts.Inputs
                .Where(x => x.PayrollSubjectId == subjectId).OrderBy(x => x.Code, StringComparer.Ordinal)
                .Select(x => new ExplainNode("INPUT", x.Code, x.ResolvedValue, x.DataType.ToString(), x.DefinitionRevision.ToString(CultureInfo.InvariantCulture))).ToArray())
        };
        children.AddRange(components.Select(x => x.ExplainTree));
        children.AddRange(funds.Select(x => x.ExplainTree));
        return new(subjectId, subject.EmployeeCode, context.PayrollPeriodId, context.SnapshotId, run.Id,
            subject, scheme?.SchemeVersion, components, funds, GetSubjectValidation(context, subjectId),
            data.Snapshot.SnapshotHash, run.ResultHash, run.EngineVersion, run.ExecutionMode, run.CorrelationId,
            new("PAYROLL_SUBJECT", subject.EmployeeCode, Hash: subjectResult.ResultHash, Children: children));
    }

    public ComponentExplanation ExplainComponent(ReviewResultContext context, PayComponentCalculationResultId componentResultId)
    {
        var data = Load(context);
        var result = data.ComponentResults.SingleOrDefault(x => x.Id == componentResultId)
            ?? throw Error(DiagnosticCodes.PayrollReviewExplainProvenanceIncomplete, ("componentResultId", componentResultId.Value));
        return BuildComponent(data, result);
    }

    public InputProvenanceExplanation GetInputProvenance(ReviewResultContext context, PayrollInputLedgerEntryId entryId)
    {
        var data = Load(context);
        return data.InputProvenance.TryGetValue(entryId, out var value) ? value
            : throw Error(DiagnosticCodes.PayrollReviewExplainProvenanceIncomplete, ("inputEntryId", entryId.Value));
    }

    public FundExplanation ExplainFundAllocation(ReviewResultContext context, FundAllocationResultId fundResultId)
    {
        var data = Load(context);
        var result = data.FundResults.SingleOrDefault(x => x.Id == fundResultId)
            ?? throw Error(DiagnosticCodes.PayrollReviewExplainProvenanceIncomplete, ("fundResultId", fundResultId.Value));
        return BuildFund(data, result);
    }

    public PayrollVarianceResult ComparePayrollPeriods(ReviewResultContext current, ReviewResultContext comparison)
    {
        var currentData = Load(current);
        var priorData = Load(comparison);
        EnsureSameCompany(current.CompanyId, comparison.CompanyId);
        var componentItems = Compare(currentData.ComparableSubjects, priorData.ComparableSubjects, false);
        var fundItems = Compare(currentData.ComparableSubjects, priorData.ComparableSubjects, true);
        var priorTotal = priorData.ComparableSubjects.SelectMany(x => x.Components).Sum(x => x.Value);
        var currentTotal = currentData.ComparableSubjects.SelectMany(x => x.Components).Sum(x => x.Value);
        return new(current, comparison, componentItems, fundItems, priorTotal, currentTotal,
            currentTotal - priorTotal, Percentage(priorTotal, currentTotal));
    }

    public IReadOnlyList<VarianceItem> GetSubjectVariance(ReviewResultContext current, ReviewResultContext comparison, PayrollSubjectId subjectId)
    {
        var result = ComparePayrollPeriods(current, comparison);
        return result.Components.Concat(result.FundedAmounts).Where(x => x.PayrollSubjectId == subjectId).ToArray();
    }

    public IReadOnlyList<VarianceItem> GetComponentVariance(ReviewResultContext current, ReviewResultContext comparison, string componentCode) =>
        ComparePayrollPeriods(current, comparison).Components.Where(x => x.Code == componentCode).ToArray();

    public FundingReview GetFundingReview(ReviewResultContext context)
    {
        var data = Load(context);
        var funds = data.FundResults.OrderBy(x => FundVersion(data, x).Code, StringComparer.Ordinal)
            .Select(x => BuildFundReview(data, x)).ToArray();
        return new(context, funds, funds.Sum(x => x.Available), funds.Sum(x => x.Demand), funds.Sum(x => x.Funded),
            funds.Sum(x => x.Unfunded), funds.Sum(x => x.Reserve), funds.Count(x => x.Unfunded > 0m),
            funds.Count(x => x.Indicator is FundReviewIndicator.PartiallyFunded or FundReviewIndicator.Deficit));
    }

    public FundReview GetFundReview(ReviewResultContext context, FundAllocationResultId fundResultId)
    {
        var data = Load(context);
        var result = data.FundResults.SingleOrDefault(x => x.Id == fundResultId)
            ?? throw Error(DiagnosticCodes.PayrollReviewExplainProvenanceIncomplete, ("fundResultId", fundResultId.Value));
        return BuildFundReview(data, result);
    }

    public PayrollPeriodReview GetPeriodReview(ReviewResultContext context)
    {
        var data = Load(context);
        return new(context, data.Snapshot.HistoricalFacts.Subjects.Count, data.SubjectResults.Count(x => x.CalculationStatus == PayrollCalculationResultStatus.SUCCEEDED),
            data.SubjectResults.Count(x => x.CalculationStatus == PayrollCalculationResultStatus.FAILED), data.ComponentResults.Count,
            GetPayrollValidationSummary(context), GetFundingReview(context));
    }

    private PayrollReviewDataset Load(ReviewResultContext context)
    {
        var data = source.GetDataset(context);
        EnsureSameCompany(context.CompanyId, data.Context.CompanyId);
        if (data.Snapshot.CompanyId != context.CompanyId || data.Snapshot.PayrollPeriodId != context.PayrollPeriodId || data.Snapshot.Id != context.SnapshotId)
            throw Error(DiagnosticCodes.PayrollReviewCrossCompanyReference, ("snapshotId", context.SnapshotId.Value));
        if (context.CalculationRunId is not null && (data.Run is null || data.Run.Id != context.CalculationRunId || data.Run.CompanyId != context.CompanyId))
            throw Error(DiagnosticCodes.PayrollReviewCalculationRunNotFound, ("calculationRunId", context.CalculationRunId.Value.Value));
        if (data.SubjectResults.Any(x => x.CompanyId != context.CompanyId) || data.ComponentResults.Any(x => x.CompanyId != context.CompanyId) || data.FundResults.Any(x => x.CompanyId != context.CompanyId))
            throw Error(DiagnosticCodes.PayrollReviewCrossCompanyReference, ("companyId", context.CompanyId.Value));
        return data;
    }

    private static ValidationFinding[] Findings(PayrollReviewDataset data) => data.Diagnostics
        .Select(x => new ValidationFinding(data.Context.CompanyId, data.Context.PayrollPeriodId, data.Context.SnapshotId,
            data.Context.CalculationRunId, x.PayrollSubjectId, x.ComponentId, x.FundResultId, x.SourceModule,
            x.Diagnostic.Severity, x.Diagnostic.Code, x.Diagnostic.Arguments, x.IsBlocking,
            x.BusinessReference, x.ExplainReference)).ToArray();

    private static PayrollCalculationRunDto RequireRun(PayrollReviewDataset data) => data.Run
        ?? throw Error(DiagnosticCodes.PayrollReviewCalculationRunNotFound, ("snapshotId", data.Context.SnapshotId.Value));

    private static ComponentExplanation BuildComponent(PayrollReviewDataset data, PayComponentCalculationResultDto result)
    {
        var inputs = result.InputLedgerEntryIds.Select(id => data.InputProvenance.TryGetValue(id, out var p) ? p : null)
            .Where(x => x is not null).Cast<InputProvenanceExplanation>().Distinct().ToArray();
        var component = data.Snapshot.PolicyConfiguration.CompensationVersions.SelectMany(x => x.Components)
            .SingleOrDefault(x => x.PayComponentId == result.PayComponentId && x.Version == result.PayComponentVersion);
        var dependencies = component?.DependsOn?.Select(x => x.Value).ToArray() ?? [];
        var nodes = inputs.Select(x => new ExplainNode("INPUT_PROVENANCE", x.InputCode, x.ResolvedValue, x.ResolvedValue.DataType.ToString(),
            Children: ProvenanceChildren(x))).ToList();
        if (result.ExplainTraceJson is not null)
            nodes.Add(new("FORMULA_EXPLAIN_TRACE", result.FormulaChecksum, VersionId: result.FormulaVersionId?.Value.ToString(),
                CanonicalArguments: new Dictionary<string, object?> { ["storedTrace"] = result.ExplainTraceJson }));
        return new(result, inputs, dependencies, new("PAY_COMPONENT", result.ComponentCode, result.ResultValue,
            result.ResultDataType, result.PayComponentVersion.ToString(CultureInfo.InvariantCulture), result.ResultHash, result.DiagnosticCode,
            Children: nodes));
    }

    private static List<ExplainNode> ProvenanceChildren(InputProvenanceExplanation input)
    {
        var result = new List<ExplainNode>();
        if (input.Attendance is not null) result.Add(new("ATTENDANCE", input.Attendance.DerivedOutputCode,
            VersionId: input.Attendance.AttendancePolicyVersionId.ToString(),
            CanonicalArguments: new Dictionary<string, object?> { ["importBatchId"] = input.Attendance.ImportBatchId, ["source"] = input.Attendance.AttendanceSource }));
        if (input.Performance is not null) result.Add(new("PERFORMANCE", input.InputCode,
            Value: PayrollInputValue.Decimal(input.Performance.FinalAchievement), VersionId: input.Performance.PerformancePolicyVersionId.ToString(),
            Hash: input.Performance.ExplainHash));
        return result;
    }

    private static FundExplanation BuildFund(PayrollReviewDataset data, FundAllocationResultDto result)
    {
        var version = FundVersion(data, result);
        var members = result.Members.OrderBy(x => x.AllocationSequence).Select(x => new ExplainNode("FUND_MEMBER", x.RequirementReference,
            PayrollInputValue.Decimal(x.AllocatedAmount), "DECIMAL", Hash: x.ResultHash,
            CanonicalArguments: new Dictionary<string, object?> { ["requested"] = x.RequestedAmount, ["eligible"] = x.EligibleAmount, ["priority"] = x.Priority, ["weight"] = x.Weight })).ToArray();
        var tree = new ExplainNode("FUND_ALLOCATION", version.Code, PayrollInputValue.Decimal(result.FundedAmount), "DECIMAL",
            version.Revision.ToString(CultureInfo.InvariantCulture), result.ResultHash, Children: members,
            CanonicalArguments: new Dictionary<string, object?> { ["available"] = result.AvailableFund, ["demand"] = result.EligibleDemand, ["unfunded"] = result.UnfundedAmount, ["reserve"] = result.ReserveAmount, ["rawCoverage"] = result.RawCoverageRatio, ["effectiveCoverage"] = result.EffectiveFundingRatio, ["allocationMethod"] = result.AllocationMethod.ToString(), ["storedTrace"] = result.ExplainTraceJson });
        return new(result, version.Code, version.Revision, tree);
    }

    private static SnapshotPayrollFundVersion FundVersion(PayrollReviewDataset data, FundAllocationResultDto result) =>
        data.Snapshot.PolicyConfiguration.FundVersions?.SingleOrDefault(x => x.FundVersionId == result.FundVersionId)
        ?? throw Error(DiagnosticCodes.PayrollReviewExplainProvenanceIncomplete, ("fundVersionId", result.FundVersionId.Value));

    private static FundReview BuildFundReview(PayrollReviewDataset data, FundAllocationResultDto result)
    {
        var version = FundVersion(data, result);
        var indicator = result.EligibleDemand == 0m ? FundReviewIndicator.NoDemand
            : result.UnfundedAmount > 0m && result.FundedAmount == 0m ? FundReviewIndicator.Deficit
            : result.UnfundedAmount > 0m ? FundReviewIndicator.PartiallyFunded : FundReviewIndicator.FullyFunded;
        return new(result.Id, version.Code, version.Revision, result.AvailableFund, result.EligibleDemand,
            result.FundedAmount, result.UnfundedAmount, result.ReserveAmount, result.RawCoverageRatio,
            result.EffectiveFundingRatio, result.AllocationMethod, result.Members.Count, indicator, result.ResultHash);
    }

    private static VarianceItem[] Compare(IReadOnlyList<SubjectComparison> current,
        IReadOnlyList<SubjectComparison> prior, bool funds)
    {
        var currentMap = Flatten(current, funds);
        var priorMap = Flatten(prior, funds);
        return currentMap.Keys.Union(priorMap.Keys).OrderBy(x => x.EmployeeCode, StringComparer.Ordinal).ThenBy(x => x.Code, StringComparer.Ordinal)
            .Select(key => MakeVariance(key.SubjectId, key.EmployeeCode, key.Code,
                priorMap.GetValueOrDefault(key), currentMap.GetValueOrDefault(key), funds)).ToArray();
    }

    private static Dictionary<(PayrollSubjectId SubjectId, string EmployeeCode, string Code), ComparableValue> Flatten(
        IReadOnlyList<SubjectComparison> subjects, bool funds) => subjects.SelectMany(subject =>
        (funds ? subject.FundedAmounts : subject.Components).Select(value => (subject, value)))
        .ToDictionary(x => (x.subject.PayrollSubjectId, x.subject.EmployeeCode, x.value.Code), x => x.value);

    private static VarianceItem MakeVariance(PayrollSubjectId subjectId, string employeeCode, string code,
        ComparableValue? prior, ComparableValue? current, bool fund)
    {
        var priorValue = prior?.Value;
        var currentValue = current?.Value;
        var delta = (currentValue ?? 0m) - (priorValue ?? 0m);
        var type = prior is null ? VarianceType.NEW : current is null ? VarianceType.REMOVED
            : delta > 0m ? VarianceType.INCREASE : delta < 0m ? VarianceType.DECREASE : VarianceType.UNCHANGED;
        return new(subjectId, employeeCode, code, priorValue, currentValue, delta,
            prior is null || current is null ? null : Percentage(prior.Value, current.Value), type,
            Drivers(prior?.Provenance, current?.Provenance, fund), prior?.Provenance, current?.Provenance);
    }

    private static List<VarianceDriver> Drivers(ValueProvenance? prior, ValueProvenance? current, bool fund)
    {
        if (prior is null || current is null) return [];
        var drivers = new List<VarianceDriver>();
        AddIfChanged(drivers, prior.InputEntryIds, current.InputEntryIds, VarianceDriver.InputChanged);
        AddIfChanged(drivers, prior.FormulaVersionId, current.FormulaVersionId, VarianceDriver.FormulaVersionChanged);
        AddIfChanged(drivers, prior.ParameterVersionIds, current.ParameterVersionIds, VarianceDriver.ParameterVersionChanged);
        AddIfChanged(drivers, prior.ComponentVersionId, current.ComponentVersionId, VarianceDriver.ComponentVersionChanged);
        AddIfChanged(drivers, prior.SchemeVersionId, current.SchemeVersionId, VarianceDriver.SchemeChanged);
        AddIfChanged(drivers, prior.AssignmentId, current.AssignmentId, VarianceDriver.AssignmentChanged);
        AddIfChanged(drivers, prior.PerformanceProvenanceId, current.PerformanceProvenanceId, VarianceDriver.PerformanceChanged);
        AddIfChanged(drivers, prior.AttendanceProvenanceId, current.AttendanceProvenanceId, VarianceDriver.AttendanceChanged);
        if (fund) AddIfChanged(drivers, prior.FundPolicyVersionId, current.FundPolicyVersionId, VarianceDriver.FundPolicyChanged);
        if (fund && prior.FundCoverage != current.FundCoverage) drivers.Add(VarianceDriver.FundCoverageChanged);
        return drivers;
    }

    private static void AddIfChanged<T>(List<VarianceDriver> result, T prior, T current, VarianceDriver driver)
    {
        var equal = prior is IEnumerable<string> priorItems && current is IEnumerable<string> currentItems
            ? priorItems.SequenceEqual(currentItems, StringComparer.Ordinal) : EqualityComparer<T>.Default.Equals(prior, current);
        if (!equal) result.Add(driver);
    }

    private static decimal? Percentage(decimal prior, decimal current) => prior == 0m ? null : (current - prior) / decimal.Abs(prior);

    private static void EnsureSameCompany(CompanyId expected, CompanyId actual)
    {
        if (expected != actual) throw Error(DiagnosticCodes.PayrollReviewCrossCompanyReference,
            ("expectedCompanyId", expected.Value), ("actualCompanyId", actual.Value));
    }

    private static PayrollReviewException Error(string code, params (string Key, object? Value)[] arguments) =>
        new(new Diagnostic(code, DiagnosticSeverity.Error, arguments.ToDictionary(x => x.Key, x => x.Value)));
}
