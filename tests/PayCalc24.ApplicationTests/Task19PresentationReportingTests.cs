using System.Text;
using PayCalc24.Client.Avalonia;
using PayCalc24.Client.Avalonia.Presentation;
using PayCalc24.Client.Avalonia.Features.Payroll;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.Presentation;
using PayCalc24.Contracts.Reporting;
using PayCalc24.Reporting.Services;

namespace PayCalc24.ApplicationTests;

public sealed class Task19PresentationReportingTests
{
    [Fact]
    public void ShellNavigationIsDeterministicAndCompanySwitchClearsCompanyState()
    {
        var root = new DesktopCompositionRoot();
        Assert.Equal(AppRoute.Dashboard, root.Shell.CurrentRoute);
        root.Shell.Navigate(AppRoute.Reports);
        Assert.Equal(AppRoute.Reports, root.Shell.CurrentRoute);

        var companyA = CompanyId.From(Guid.NewGuid()); var companyB = CompanyId.From(Guid.NewGuid());
        root.Company.Switch(companyA);
        root.Payroll.Open(new(companyA, PayrollPeriodId.From(Guid.NewGuid()), new(2026, 7, 31),
            PayrollCalculationSnapshotId.From(Guid.NewGuid()), 1, PayrollCalculationRunId.From(Guid.NewGuid()), "APPROVED"));
        root.Payroll.Select(PayrollSubjectId.From(Guid.NewGuid()));
        root.Company.Switch(companyB);
        Assert.Null(root.Payroll.Revision); Assert.Null(root.Payroll.SelectedSubject);
    }

    [Fact]
    public void HistoricalRevisionRemainsPinnedAndCultureThemeDoNotAlterIt()
    {
        var root = new DesktopCompositionRoot(); var company = CompanyId.From(Guid.NewGuid()); root.Company.Switch(company);
        var revision = new PayrollRevisionContext(company, PayrollPeriodId.From(Guid.NewGuid()), new(2026, 7, 31),
            PayrollCalculationSnapshotId.From(Guid.NewGuid()), 1, PayrollCalculationRunId.From(Guid.NewGuid()), "LOCKED");
        root.Payroll.Open(revision);
        root.Culture.Select("vi-VN"); root.Appearance.Select(ThemeMode.DARK);
        Assert.Equal(1, root.Payroll.Revision!.SnapshotRevision);
        Assert.Equal("Báo cáo", root.Shell.LocalizedItems.Single(x => x.Route == AppRoute.Reports).Label);
        Assert.Equal(ThemeMode.DARK, root.Appearance.Mode);
    }

    [Fact]
    public void IconCatalogResolvesEveryKeyAndUnknownDiagnosticIsSafe()
    {
        var icons = new SvgIconProvider();
        foreach (var key in Enum.GetValues<IconKey>()) Assert.True(icons.Resolve(key).AssetUri.IsAbsoluteUri);
        Assert.Equal(IconKey.Missing, icons.Resolve((IconKey)999).Key);
        var presenter = new DiagnosticPresenter(new DesktopLocalizationService(new DesktopResourceProvider()));
        var result = presenter.Present(new("PAYROLL.UNKNOWN", DiagnosticSeverity.Warning, new Dictionary<string, object?>()), "vi-VN");
        Assert.Contains("PAYROLL.UNKNOWN", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainPresentationExposesImmutableTask15And18ValuesWithoutAnEngine()
    {
        var viewModel = new PayrollSubjectDetailViewModel([], [], [], null, null, "immutable-provenance");
        Assert.Empty(viewModel.Components); Assert.Empty(viewModel.Funds); Assert.Empty(viewModel.Statutory);
        Assert.Null(viewModel.NetPay); Assert.Null(viewModel.EmployerCost);
        Assert.Equal("immutable-provenance", viewModel.ProvenanceHash);
    }

    [Fact]
    public void ReportsPinIdentityAndFormatCultureWithoutChangingCanonicalSource()
    {
        var request = Request("en-US"); var source = new FakeReportSource();
        var service = new PayrollReportService(source, new DeterministicPortableTextRenderer());
        var english = service.Generate(request);
        var vietnamese = service.Generate(request with { Culture = "vi-VN" });
        Assert.Equal(1, english.Provenance.SnapshotRevision);
        Assert.Equal(request.CalculationRunId, english.Provenance.CalculationRunId);
        Assert.Equal("canonical-source", english.Provenance.SourceHash);
        Assert.NotEqual(Encoding.UTF8.GetString(english.Content), Encoding.UTF8.GetString(vietnamese.Content));
        Assert.Equal("canonical-source", vietnamese.Provenance.SourceHash);
    }

    [Theory]
    [InlineData(PayrollReportType.PayrollSummary)]
    [InlineData(PayrollReportType.PayrollSubjectDetail)]
    [InlineData(PayrollReportType.PayrollSettlementSummary)]
    public void MinimumReportTypesRender(PayrollReportType type)
    {
        var result = new PayrollReportService(new FakeReportSource(), new DeterministicPortableTextRenderer())
            .Generate(Request("en-US") with { ReportType = type, PayrollSubjectId = type == PayrollReportType.PayrollSubjectDetail ? PayrollSubjectId.From(Guid.NewGuid()) : null });
        Assert.NotEmpty(result.Content); Assert.Equal(64, result.ResultHash.Length);
    }

    private static PayrollReportRequest Request(string culture) => new(CompanyId.From(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        PayrollPeriodId.From(Guid.Parse("22222222-2222-2222-2222-222222222222")),
        PayrollCalculationSnapshotId.From(Guid.Parse("33333333-3333-3333-3333-333333333333")), 1,
        PayrollCalculationRunId.From(Guid.Parse("44444444-4444-4444-4444-444444444444")),
        PayrollReportType.PayrollSummary, culture, PayrollReportOutputFormat.PortableText);

    private sealed class FakeReportSource : IPayrollReportSource
    {
        public PayrollReportSource GetSource(PayrollReportRequest request) => new(request, "canonical-source", new(2026, 7, 31),
            new(2, 2, 1234.56m, 1200m, 0),
            new("E001", [], [], [], null, null, "explain-hash"), new(2, 1100m, 1400m, ["h2", "h1"]));
    }
}
