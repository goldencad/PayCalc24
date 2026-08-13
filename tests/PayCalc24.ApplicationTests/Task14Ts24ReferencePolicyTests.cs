using System.Globalization;
using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollFunds;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.FormulaEngine.Execution;
using PayCalc24.PayrollCalculation.Services;
using PayCalc24.PayrollFunds.Services;

namespace PayCalc24.ApplicationTests;

/// <summary>
/// Executable reference configuration. Business codes deliberately live in test scope; every
/// execution path below uses the production generic snapshot, formula, calculation and fund engines.
/// </summary>
public sealed class Task14Ts24ReferencePolicyTests
{
    [Theory]
    [InlineData(.60, 2_000_000)]
    [InlineData(.85, 4_000_000)]
    [InlineData(1.00, 6_000_000)]
    [InlineData(1.10, 7_000_000)]
    [InlineData(1.30, 8_000_000)]
    public async Task ConfiguredP3CurveCoversFloorInterpolationTargetAndCap(double achievement, decimal expected)
    {
        var policy = Ts24ReferencePolicyBuilder.Ts24([((decimal)achievement, true)]);
        var result = await policy.Calculate(PayrollExecutionMode.Replay, $"curve-{achievement.ToString(CultureInfo.InvariantCulture)}");
        Assert.Equal(expected, policy.Components(result).Single(x => x.ComponentCode == "P3").ResultValue!.DecimalValue);
    }

    [Fact]
    public async Task AttendanceGateFailureIsConsumedAsCanonicalInputsAndProducesZero()
    {
        var policy = Ts24ReferencePolicyBuilder.Ts24([(1.20m, false)]);
        var run = await policy.Calculate(PayrollExecutionMode.Replay, "gate-failure");
        var p3 = policy.Components(run).Single(x => x.ComponentCode == "P3");

        Assert.Equal(0m, p3.ResultValue!.DecimalValue);
        Assert.Contains(policy.AttendanceEntryId, p3.InputLedgerEntryIds);
        Assert.Contains(policy.PerformanceEntryIds.Single(), p3.InputLedgerEntryIds);
        Assert.Contains("P3_ELIGIBILITY", p3.ExplainTraceJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndToEndSchemeCalculatesP1P2P3ThenKeepsFundedP3Separate()
    {
        var policy = Ts24ReferencePolicyBuilder.Ts24([(.85m, true), (1.10m, true), (1.30m, true)]);
        var run = await policy.Calculate(PayrollExecutionMode.Replay, "reference-run");
        var componentResults = policy.Components(run);
        Assert.Equal(9, componentResults.Count);
        Assert.Equal([4_000_000m, 8_000_000m, 12_000_000m], componentResults.Where(x => x.ComponentCode == "P3").Select(x => x.ResultValue!.DecimalValue));

        var funded = await policy.Allocate(run, 18_000_000m, "under-funded");
        Assert.Equal(24_000_000m, funded.EligibleDemand);
        Assert.Equal(18_000_000m, funded.FundedAmount);
        Assert.Equal(6_000_000m, funded.UnfundedAmount);
        Assert.Equal([3_000_000m, 6_000_000m, 9_000_000m], funded.Members.Select(x => x.AllocatedAmount));
        Assert.Equal([4_000_000m, 8_000_000m, 12_000_000m], componentResults.Where(x => x.ComponentCode == "P3").Select(x => x.ResultValue!.DecimalValue));

        var fullyFunded = await policy.Allocate(run, 25_000_000m, "fully-funded");
        Assert.Equal(24_000_000m, fullyFunded.FundedAmount);
        Assert.Equal(1_000_000m, fullyFunded.ReserveAmount);
        var zero = await policy.Allocate(run, 1_000_000m, "zero-demand", []);
        Assert.Equal(1m, zero.RawCoverageRatio);
        Assert.Equal(0m, zero.FundedAmount);
    }

    [Fact]
    public async Task FrozenReplaySurvivesPolicyAndFactCorrectionsWhileBackTestAndWhatIfAreExplicit()
    {
        var policy = Ts24ReferencePolicyBuilder.Ts24([(.85m, true)]);
        var historical = await policy.Calculate(PayrollExecutionMode.Replay, "historical");
        var historicalP3 = policy.Components(historical).Single(x => x.ComponentCode == "P3");

        policy.ChangeLiveFacts(1.20m, false);
        var replay = await policy.Calculate(PayrollExecutionMode.Replay, "replay-after-corrections");
        Assert.Equal(historicalP3.ResultValue, policy.Components(replay).Single(x => x.ComponentCode == "P3").ResultValue);
        Assert.Equal(historicalP3.ResultHash, policy.Components(replay).Single(x => x.ComponentCode == "P3").ResultHash);

        var alternative = policy.PolicyWithThresholds(.60m, .95m, 1.30m, revision: 2);
        var backTest = await policy.Calculate(PayrollExecutionMode.BackTest, "back-test", alternative);
        var whatIf = await policy.Calculate(PayrollExecutionMode.WhatIf, "what-if", alternative);
        Assert.Equal(4_857_142.8571428571428571428571m, policy.Components(backTest).Single(x => x.ComponentCode == "P3").ResultValue!.DecimalValue);
        Assert.Equal(policy.Components(backTest).Single(x => x.ComponentCode == "P3").ResultValue, policy.Components(whatIf).Single(x => x.ComponentCode == "P3").ResultValue);
        Assert.Equal(4_000_000m, historicalP3.ResultValue!.DecimalValue);
        Assert.NotEqual(historical.ResultHash, backTest.ResultHash);
    }

    [Fact]
    public async Task SameEnginesExecuteCompanyBWithDifferentCodesThresholdsAndFund()
    {
        var policy = Ts24ReferencePolicyBuilder.CompanyB(.80m);
        var run = await policy.Calculate(PayrollExecutionMode.Replay, "company-b");
        var results = policy.Components(run);
        Assert.Equal(["BASE", "ATT_ALLOWANCE", "PERFORMANCE_BONUS"], results.Select(x => x.ComponentCode));
        Assert.Equal(4_666_666.6666666666666666666667m, results.Single(x => x.ComponentCode == "PERFORMANCE_BONUS").ResultValue!.DecimalValue);
        var funded = await policy.Allocate(run, 2_000_000m, "company-b-fund");
        Assert.Equal("COMPANY_B_BONUS_POOL", policy.Fund.Code);
        Assert.Equal(2_000_000m, funded.FundedAmount);
    }

    [Fact]
    public async Task CalculationAndFundingHashesAreCultureIndependentAndCompanyIsolated()
    {
        var policy = Ts24ReferencePolicyBuilder.Ts24([(.85m, true), (1.10m, true)]);
        var original = CultureInfo.CurrentCulture;
        try
        {
            var hashes = new List<(string? Run, string Fund)>();
            foreach (var culture in new[] { "vi-VN", "en-US", "fr-FR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var run = await policy.Calculate(PayrollExecutionMode.Replay, $"culture-{culture}");
                var fund = await policy.Allocate(run, 8_250_000m, $"fund-{culture}");
                hashes.Add((run.ResultHash, fund.ResultHash));
            }
            Assert.Single(hashes.Select(x => x.Run).Distinct());
            Assert.Single(hashes.Select(x => x.Fund).Distinct());
        }
        finally { CultureInfo.CurrentCulture = original; }

        var otherCompany = CompanyId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Assert.ThrowsAny<Exception>(() => policy.Periods.GetSnapshotById(otherCompany, policy.Snapshot.Id));
        await Assert.ThrowsAsync<PayrollFundException>(async () => await policy.Funds.CalculateAsync(
            new(otherCompany, policy.Snapshot.Id, policy.Fund.FundVersionId, PayrollExecutionMode.Replay, "cross-company", [])));
    }

    private sealed class Ts24ReferencePolicyBuilder : IPayrollSnapshotResolver
    {
        private readonly string[] componentCodes;
        private readonly string baseInputCode;
        private readonly string attendanceInputCode;
        private readonly string achievementInputCode;
        private readonly string eligibilityInputCode;
        private readonly string bonusCode;
        private readonly decimal[] achievements;
        private readonly bool[] eligibility;
        private decimal liveAchievement;
        private bool liveEligibility;

        public CompanyId Company { get; }
        public PayrollPeriodService Periods { get; }
        public PayrollCalculationService Calculation { get; }
        public PayrollFundCalculationService Funds { get; }
        public PayrollCalculationSnapshotDto Snapshot { get; private set; } = null!;
        public SnapshotPayrollFundVersion Fund { get; }
        public PayrollInputLedgerEntryId AttendanceEntryId { get; } = PayrollInputLedgerEntryId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"));
        public PayrollInputLedgerEntryId[] PerformanceEntryIds { get; }

        private Ts24ReferencePolicyBuilder(CompanyId company, string[] codes, string baseInput, string attendanceInput,
            string achievementInput, string eligibilityInput, string fundCode, decimal[] achievements, bool[] eligibility,
            decimal floorThreshold, decimal targetThreshold, decimal maxThreshold)
        {
            Company = company; componentCodes = codes; baseInputCode = baseInput; attendanceInputCode = attendanceInput;
            achievementInputCode = achievementInput; eligibilityInputCode = eligibilityInput; bonusCode = codes[2];
            this.achievements = achievements; this.eligibility = eligibility; liveAchievement = achievements[0]; liveEligibility = eligibility[0];
            PerformanceEntryIds = achievements.Select((_, i) => PayrollInputLedgerEntryId.From(Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-{i + 1:000000000000}"))).ToArray();
            Fund = new(PayrollFundVersionId.From(Guid.Parse("f0000000-0000-0000-0000-000000000002")), PayrollFundDefinitionId.From(Guid.Parse("f0000000-0000-0000-0000-000000000001")), fundCode, 1, PayrollFundType.BONUS, new(FundScopeType.ORGANIZATION, "OrganizationUnit", Guid.Parse("10000000-0000-0000-0000-000000000001")), new(FundSourceType.FIXED, 25_000_000m), new(FundAllocationMethod.PROPORTIONAL, 2));
            var context = new Context(company); var user = new User(); var correlation = new Correlation();
            Periods = new(context, user, correlation, new Audit(), TimeProvider.System, this);
            Calculation = new(context, user, correlation, TimeProvider.System, Periods, Periods);
            Funds = new(context, correlation, TimeProvider.System, Periods, new SafeFormulaEngine());
            InitialPolicy = CreatePolicy(floorThreshold, targetThreshold, maxThreshold, 1);
        }

        private SnapshotPolicyConfiguration InitialPolicy { get; }
        public static Ts24ReferencePolicyBuilder Ts24((decimal Achievement, bool Eligible)[] people) =>
            new(CompanyId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), ["P1", "P2", "P3"], "P1_AMOUNT", "ATTENDANCE_SCORE", "FINAL_ACHIEVEMENT", "P3_ELIGIBILITY", "IT_P3_POOL", people.Select(x => x.Achievement).ToArray(), people.Select(x => x.Eligible).ToArray(), .70m, 1m, 1.20m);
        public static Ts24ReferencePolicyBuilder CompanyB(decimal achievement) =>
            new(CompanyId.From(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")), ["BASE", "ATT_ALLOWANCE", "PERFORMANCE_BONUS"], "BASE_RATE", "PRESENCE_POINTS", "MERIT_INDEX", "BONUS_ALLOWED", "COMPANY_B_BONUS_POOL", [achievement], [true], .60m, .90m, 1.30m);

        public async Task<PayrollCalculationRunDto> Calculate(PayrollExecutionMode mode, string key, SnapshotPolicyConfiguration? alternative = null)
        {
            if (Snapshot is null)
            {
                var period = await Periods.CreateAsync(new(Company, "2026-08", null, new(2026, 8, 1), new(2026, 8, 31), new(2026, 8, 31)));
                period = await Periods.PrepareAsync(Company, period.Id, period.Revision);
                Snapshot = await Periods.FreezeAsync(Company, period.Id, period.Revision, "freeze-reference-policy");
            }
            return await Calculation.StartAsync(new(Company, Snapshot.Id, mode, key, Snapshot.SnapshotHash, alternative));
        }

        public IReadOnlyList<PayComponentCalculationResultDto> Components(PayrollCalculationRunDto run) => Calculation.ListComponentResults(Company, run.Id);
        public SnapshotPolicyConfiguration PolicyWithThresholds(decimal floor, decimal target, decimal maximum, int revision) => CreatePolicy(floor, target, maximum, revision);
        public void ChangeLiveFacts(decimal achievement, bool eligible) { liveAchievement = achievement; liveEligibility = eligible; }

        public async Task<FundAllocationResultDto> Allocate(PayrollCalculationRunDto run, decimal available, string key, IReadOnlyList<FundRequirement>? explicitRequirements = null)
        {
            var requirements = explicitRequirements ?? Components(run).Where(x => x.ComponentCode == bonusCode).Select(x =>
                new FundRequirement($"{x.PayrollSubjectId.Value:D}:{bonusCode}", Company, x.ResultValue!.DecimalValue!.Value, x.PayrollSubjectId, x.PayComponentId.Value, ProvenanceType: "CALCULATION_RESULT", ProvenanceIds: [x.Id.Value])).ToArray();
            return await Funds.CalculateAsync(new(Company, Snapshot.Id, Fund.FundVersionId, PayrollExecutionMode.WhatIf, key, requirements, run.Id, Guid.NewGuid().ToString(), available));
        }

        public PayrollSnapshotCandidate Resolve(CompanyId companyId, PayrollPeriodId payrollPeriodId, DateOnly businessDate)
        {
            if (companyId != Company) throw new InvalidOperationException("Company isolation violation.");
            var org = OrganizationUnitId.From(Guid.Parse("10000000-0000-0000-0000-000000000001"));
            var subjects = achievements.Select((_, i) => new SnapshotSubjectFact(Subject(i), $"E{i + 1:000}", PayrollAssignmentId.From(Guid.Parse($"20000000-0000-0000-0000-{i + 1:000000000000}")), org, null, null, SchemeId, new(2026, 1, 1), null, 0, [])).ToArray();
            var inputs = subjects.SelectMany((subject, i) => Inputs(subject.PayrollSubjectId, i)).ToArray();
            return new(companyId, new(subjects, inputs), InitialPolicy, []);
        }

        private IReadOnlyList<SnapshotResolvedInput> Inputs(PayrollSubjectId subject, int index)
        {
            var achievement = index == 0 ? liveAchievement : achievements[index];
            var eligible = index == 0 ? liveEligibility : eligibility[index];
            return
            [
                Input(subject, baseInputCode, PayrollInputValue.Decimal(12_000_000m), PayrollInputUnitType.AMOUNT, Entry(index, 1)),
                Input(subject, attendanceInputCode, PayrollInputValue.Decimal(1_000_000m), PayrollInputUnitType.AMOUNT, index == 0 ? AttendanceEntryId : Entry(index, 2)),
                Input(subject, achievementInputCode, PayrollInputValue.Decimal(achievement), PayrollInputUnitType.PERCENT, PerformanceEntryIds[index]),
                Input(subject, eligibilityInputCode, PayrollInputValue.Boolean(eligible), PayrollInputUnitType.NONE, Entry(index, 4)),
                Input(subject, $"{bonusCode}_FLOOR", PayrollInputValue.Decimal(2_000_000m), PayrollInputUnitType.AMOUNT, Entry(index, 5)),
                Input(subject, $"{bonusCode}_TARGET", PayrollInputValue.Decimal(6_000_000m), PayrollInputUnitType.AMOUNT, Entry(index, 6)),
                Input(subject, $"{bonusCode}_MAXIMUM", PayrollInputValue.Decimal(8_000_000m + index * 2_000_000m), PayrollInputUnitType.AMOUNT, Entry(index, 7))
            ];
        }

        private SnapshotPolicyConfiguration CreatePolicy(decimal floor, decimal target, decimal maximum, int revision)
        {
            var p1 = ComponentId(1); var p2 = ComponentId(2); var p3 = ComponentId(3);
            var formulaDefinition = FormulaDefinitionId.From(Guid.Parse("30000000-0000-0000-0000-000000000001"));
            var formulaVersion = FormulaVersionId.From(Guid.Parse($"30000000-0000-0000-0000-{revision:000000000000}"));
            var parameters = new SnapshotParameterVersion(ParameterSetVersionId.From(Guid.Parse($"40000000-0000-0000-0000-{revision:000000000000}")), "P3_CURVE", revision,
            [
                Parameter($"{bonusCode}_FLOOR_THRESHOLD", floor), Parameter($"{bonusCode}_TARGET_THRESHOLD", target), Parameter($"{bonusCode}_MAX_THRESHOLD", maximum)
            ]);
            var components = new[]
            {
                new SnapshotPayComponentVersion(p1, 1, 10, CalculationMethod.INPUT, null, componentCodes[0], true, baseInputCode),
                new SnapshotPayComponentVersion(p2, 1, 20, CalculationMethod.INPUT, null, componentCodes[1], true, attendanceInputCode),
                new SnapshotPayComponentVersion(p3, revision, 30, CalculationMethod.FORMULA, formulaDefinition, componentCodes[2], true)
            };
            return new([new(SchemeId, revision, components)], [new(formulaDefinition, formulaVersion, revision, new string((char)('a' + revision), 64), FormulaExpression())], [parameters], [], [], [Fund with { Revision = revision }]);
        }

        private string FormulaExpression()
        {
            var floor = $"{bonusCode}_FLOOR"; var target = $"{bonusCode}_TARGET"; var maximum = $"{bonusCode}_MAXIMUM";
            var floorThreshold = $"{bonusCode}_FLOOR_THRESHOLD"; var targetThreshold = $"{bonusCode}_TARGET_THRESHOLD"; var maxThreshold = $"{bonusCode}_MAX_THRESHOLD";
            return $"IF({eligibilityInputCode} = FALSE, 0.0, IF({achievementInputCode} <= {floorThreshold}, {floor}, IF({achievementInputCode} < {targetThreshold}, INTERPOLATE({achievementInputCode}, {floorThreshold}, {floor}, {targetThreshold}, {target}), IF({achievementInputCode} < {maxThreshold}, INTERPOLATE({achievementInputCode}, {targetThreshold}, {target}, {maxThreshold}, {maximum}), {maximum}))))";
        }

        private ParameterValueDto Parameter(string code, decimal value)
        {
            var version = ParameterSetVersionId.From(Guid.Parse("40000000-0000-0000-0000-000000000001"));
            return new(Guid.NewGuid(), Company, version, code, null, FormulaTypedValue.Decimal(value), "RATIO", null, null);
        }
        private static SnapshotResolvedInput Input(PayrollSubjectId subject, string code, PayrollInputValue value, PayrollInputUnitType unit, PayrollInputLedgerEntryId entry) =>
            new(subject, PayrollInputDefinitionId.From(StableGuid(code)), 1, code, value.DataType, unit, PayrollInputAggregationType.LATEST, value, [entry]);
        private static PayrollInputLedgerEntryId Entry(int person, int value) => PayrollInputLedgerEntryId.From(Guid.Parse($"50000000-0000-0000-{person + 1:0000}-{value:000000000000}"));
        private static PayrollSubjectId Subject(int index) => PayrollSubjectId.From(Guid.Parse($"60000000-0000-0000-0000-{index + 1:000000000000}"));
        private static Guid StableGuid(string value) { var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)); return new Guid(bytes[..16]); }
        private static readonly CompensationSchemeId SchemeId = CompensationSchemeId.From(Guid.Parse("70000000-0000-0000-0000-000000000001"));
        private static PayComponentId ComponentId(int value) => PayComponentId.From(Guid.Parse($"80000000-0000-0000-0000-{value:000000000000}"));
    }

    private sealed record Context(CompanyId CompanyId) : ICompanyContext;
    private sealed class User : ICurrentUser { public UserId UserId { get; } = UserId.From(Guid.Parse("90000000-0000-0000-0000-000000000001")); public bool HasPermission(string permissionCode) => true; }
    private sealed class Correlation : ICorrelationContext { public string CorrelationId => "task-14-reference-policy"; public string? IdempotencyKey => null; }
    private sealed class Audit : IAuditWriter { public ValueTask WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) => ValueTask.CompletedTask; }
}
