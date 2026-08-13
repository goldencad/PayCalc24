using System.Globalization;
using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollFunds;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.PayrollReview;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.PayrollReview.Services;

namespace PayCalc24.ApplicationTests;

public sealed class Task15PayrollReviewTests
{
    [Fact]
    public void AggregatesCanonicalDiagnosticsAndBlockingSemantics()
    {
        var data = Dataset();
        data = data with { Diagnostics =
        [
            Source("ATTENDANCE", "ATTENDANCE.WARNING", DiagnosticSeverity.Warning),
            Source("PERFORMANCE", "PERFORMANCE.INFO", DiagnosticSeverity.Info),
            Source("FORMULA_ENGINE", "FORMULA_ENGINE.ERROR", DiagnosticSeverity.Error, true),
            Source("PAYROLL_FUND", "PAYROLL_FUND.WARNING", DiagnosticSeverity.Warning)
        ]};
        var service = Service(data);

        var summary = service.GetPayrollValidationSummary(data.Context);

        Assert.Equal((1, 2, 1), (summary.ErrorCount, summary.WarningCount, summary.InfoCount));
        Assert.True(summary.Blocking);
        Assert.False(summary.ReadyForReview);
        Assert.Equal("FORMULA_ENGINE.ERROR", summary.Findings[2].DiagnosticCode);
        Assert.Equal("v", summary.Findings[2].CanonicalArguments["key"]);
    }

    [Fact]
    public void VarianceIsExplicitSafeForZeroAndDetectsAddedRemovedAndStructuralDrivers()
    {
        var company = CompanyId.From(Guid.NewGuid());
        var subject = PayrollSubjectId.From(Guid.NewGuid());
        var prior = Dataset(company: company, period: PayrollPeriodId.From(Guid.NewGuid()), components:
        [Subject(subject, "E1", [Value("BASE", 0m, "f1", ["i1"], ["p1"]), Value("BONUS", 10m)], [])]);
        var current = Dataset(company: company, period: PayrollPeriodId.From(Guid.NewGuid()), components:
        [Subject(subject, "E1", [Value("BASE", 100m, "f2", ["i2"], ["p2"]), Value("ALLOWANCE", 5m)], [])]);
        var service = Service(prior, current);

        var result = service.ComparePayrollPeriods(current.Context, prior.Context);

        var baseline = Assert.Single(result.Components, x => x.Code == "BASE");
        Assert.Equal(100m, baseline.AbsoluteDelta);
        Assert.Null(baseline.PercentageDelta);
        Assert.Contains(VarianceDriver.InputChanged, baseline.Drivers);
        Assert.Contains(VarianceDriver.FormulaVersionChanged, baseline.Drivers);
        Assert.Contains(VarianceDriver.ParameterVersionChanged, baseline.Drivers);
        Assert.Equal(VarianceType.NEW, Assert.Single(result.Components, x => x.Code == "ALLOWANCE").VarianceType);
        Assert.Equal(VarianceType.REMOVED, Assert.Single(result.Components, x => x.Code == "BONUS").VarianceType);
    }

    [Fact]
    public void ExplainUsesStoredComponentTraceAndPinnedInputProvenance()
    {
        var data = Dataset(withCalculatedSubject: true);
        var entryId = PayrollInputLedgerEntryId.From(Guid.NewGuid());
        var component = Component(data, entryId, "{\"nodeType\":\"REFERENCE\",\"code\":\"HOURS\"}");
        var input = new InputProvenanceExplanation("HOURS", PayrollInputValue.Decimal(160m), PayrollInputUnitType.HOURS,
            PayrollInputAggregationType.SUM, [entryId], "CLOCK", "BATCH-1", [],
            new(Guid.NewGuid(), "CLOCK", Guid.NewGuid(), "HOURS", [Guid.NewGuid()], []), null);
        data = data with { ComponentResults = [component], InputProvenance = new Dictionary<PayrollInputLedgerEntryId, InputProvenanceExplanation> { [entryId] = input } };
        var service = Service(data);

        var explain = service.ExplainComponent(data.Context, component.Id);

        Assert.Equal("FORMULA_EXPLAIN_TRACE", Assert.Single(explain.ExplainTree.Children!, x => x.NodeType == "FORMULA_EXPLAIN_TRACE").NodeType);
        Assert.NotNull(Assert.Single(explain.Inputs).Attendance);
        Assert.Equal(component.ResultHash, explain.ExplainTree.Hash);
    }

    [Fact]
    public void FundingReviewPreservesImmutableDeficitAndAllocation()
    {
        var data = Dataset();
        var fund = Fund(data, 18_000_000m, 24_000_000m);
        data = data with { FundResults = [fund], Snapshot = data.Snapshot with
        {
            PolicyConfiguration = data.Snapshot.PolicyConfiguration with
            {
                FundVersions = [FundVersion(fund.FundVersionId, fund.FundDefinitionId, "TEAM_BONUS")]
            }
        }};

        var review = Service(data).GetFundingReview(data.Context);

        var item = Assert.Single(review.Funds);
        Assert.Equal(18_000_000m, item.Funded);
        Assert.Equal(6_000_000m, item.Unfunded);
        Assert.Equal(FundReviewIndicator.PartiallyFunded, item.Indicator);
        Assert.Equal(1, review.DeficitCount);
    }

    [Fact]
    public void RejectsCrossCompanyData()
    {
        var data = Dataset();
        var foreign = data with { Snapshot = data.Snapshot with { CompanyId = CompanyId.From(Guid.NewGuid()) } };
        var error = Assert.Throws<PayrollReviewException>(() => Service(foreign).GetPayrollValidationSummary(data.Context));
        Assert.Equal(DiagnosticCodes.PayrollReviewCrossCompanyReference, error.Diagnostic.Code);
    }

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public void CanonicalReviewIsCultureIndependent(string culture)
    {
        using var scope = new CultureScope(culture);
        var data = Dataset(components: [Subject(PayrollSubjectId.From(Guid.NewGuid()), "E1", [Value("X", 12.5m)], [])]);
        var result = Service(data).ComparePayrollPeriods(data.Context, data.Context);
        Assert.Equal(0m, Assert.Single(result.Components).AbsoluteDelta);
        Assert.Equal(VarianceType.UNCHANGED, Assert.Single(result.Components).VarianceType);
    }

    private static ReviewSourceDiagnostic Source(string module, string code, DiagnosticSeverity severity, bool blocking = false) =>
        new(module, new Diagnostic(code, severity, new Dictionary<string, object?> { ["key"] = "v" }), blocking);

    private static PayrollReviewService Service(params PayrollReviewDataset[] datasets) => new(new FakeSource(datasets));

    private static SubjectComparison Subject(PayrollSubjectId id, string code, IReadOnlyList<ComparableValue> components, IReadOnlyList<ComparableValue> funds) => new(id, code, components, funds);
    private static ComparableValue Value(string code, decimal value, string formula = "f1", IReadOnlyList<string>? inputs = null, IReadOnlyList<string>? parameters = null) =>
        new(code, value, new("hash-" + code, formula, inputs ?? [], parameters ?? [], "component-v1", "scheme-v1", "assignment-v1", null, null, null, null));

    private static PayrollReviewDataset Dataset(CompanyId? company = null, PayrollPeriodId? period = null,
        IReadOnlyList<SubjectComparison>? components = null, bool withCalculatedSubject = false)
    {
        var c = company ?? CompanyId.From(Guid.NewGuid());
        var p = period ?? PayrollPeriodId.From(Guid.NewGuid());
        var snapshotId = PayrollCalculationSnapshotId.From(Guid.NewGuid());
        var runId = PayrollCalculationRunId.From(Guid.NewGuid());
        var context = new ReviewResultContext(c, p, snapshotId, runId);
        var subjectId = PayrollSubjectId.From(Guid.NewGuid());
        var schemeId = CompensationSchemeId.From(Guid.NewGuid());
        var snapshot = new PayrollCalculationSnapshotDto(snapshotId, c, p, 1, PayrollExecutionMode.Production,
            new DateOnly(2026, 7, 31), DateTimeOffset.UnixEpoch, UserId.From(Guid.NewGuid()), DateTimeOffset.UnixEpoch,
            UserId.From(Guid.NewGuid()), "population", "inputs", "config", "snapshot-hash",
            new([new(subjectId, "E1", PayrollAssignmentId.From(Guid.NewGuid()), OrganizationUnitId.From(Guid.NewGuid()), null, null,
                schemeId, new DateOnly(2026, 7, 1), null, 0, [])], []),
            new([new(schemeId, 1, [])], [], [], [], [], []));
        var run = new PayrollCalculationRunDto(runId, c, p, snapshotId, 1, PayrollExecutionMode.Production, "1.0.0",
            PayrollCalculationRunStatus.SUCCEEDED, DateTimeOffset.UnixEpoch, UserId.From(Guid.NewGuid()), DateTimeOffset.UnixEpoch,
            UserId.From(Guid.NewGuid()), "correlation", "key", "snapshot-hash", "run-hash", null, true);
        IReadOnlyList<PayrollSubjectCalculationResultDto> subjects = withCalculatedSubject
            ? [new(PayrollSubjectCalculationResultId.From(Guid.NewGuid()), runId, c, subjectId, "E1", 1,
                PayrollCalculationResultStatus.SUCCEEDED, "subject-hash", null, DateTimeOffset.UnixEpoch)] : [];
        return new(context, snapshot, run, subjects, [], [], [],
            new Dictionary<PayrollInputLedgerEntryId, InputProvenanceExplanation>(), components ?? []);
    }

    private static PayComponentCalculationResultDto Component(PayrollReviewDataset data, PayrollInputLedgerEntryId inputId, string trace) =>
        new(PayComponentCalculationResultId.From(Guid.NewGuid()), data.Run!.Id, data.Context.CompanyId, data.Context.PayrollPeriodId,
            data.Context.SnapshotId, data.Snapshot.HistoricalFacts.Subjects[0].PayrollSubjectId,
            data.Snapshot.PolicyConfiguration.CompensationVersions[0].CompensationSchemeId, PayComponentId.From(Guid.NewGuid()), 1,
            "HOURLY", 1, CalculationMethod.FORMULA, PayrollCalculationResultStatus.SUCCEEDED, PayrollInputValue.Decimal(100m),
            "DECIMAL", FormulaDefinitionId.From(Guid.NewGuid()), FormulaVersionId.From(Guid.NewGuid()), "formula-checksum", trace, null,
            [inputId], [ParameterSetVersionId.From(Guid.NewGuid())], [], [], "1.0.0", PayrollExecutionMode.Production,
            "correlation", "component-hash", DateTimeOffset.UnixEpoch);

    private static FundAllocationResultDto Fund(PayrollReviewDataset data, decimal available, decimal demand)
    {
        var funded = decimal.Min(available, demand);
        return new(FundAllocationResultId.From(Guid.NewGuid()), data.Context.CompanyId, data.Context.PayrollPeriodId,
            data.Context.SnapshotId, 1, data.Context.CalculationRunId, PayrollFundDefinitionId.From(Guid.NewGuid()),
            PayrollFundVersionId.From(Guid.NewGuid()), new(FundScopeType.COMPANY), available, demand, funded,
            demand - funded, available - funded, available / demand, funded / demand, FundAllocationMethod.PROPORTIONAL,
            PayrollExecutionMode.Production, "1.0.0", "correlation", null, "fund-key", "snapshot-hash", "{}", "{}", "fund-hash",
            DateTimeOffset.UnixEpoch, []);
    }

    private static SnapshotPayrollFundVersion FundVersion(PayrollFundVersionId versionId, PayrollFundDefinitionId definitionId, string code) =>
        new(versionId, definitionId, code, 1, PayrollFundType.BONUS, new(FundScopeType.COMPANY),
            new(FundSourceType.FIXED, FixedAmount: 18_000_000m), new(FundAllocationMethod.PROPORTIONAL));

    private sealed class FakeSource(IEnumerable<PayrollReviewDataset> datasets) : IPayrollReviewSource
    {
        private readonly IReadOnlyList<PayrollReviewDataset> items = datasets.ToArray();
        public PayrollReviewDataset GetDataset(ReviewResultContext context) => items.Single(x => x.Context == context);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo prior = CultureInfo.CurrentCulture;
        public CultureScope(string culture) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        public void Dispose() => CultureInfo.CurrentCulture = prior;
    }
}
