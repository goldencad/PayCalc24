using PayCalc24.Client.Avalonia;
using PayCalc24.Client.Avalonia.Presentation;

namespace PayCalc24.ApplicationTests;

public sealed class Task20COperationalWorkspaceTests
{
    [Fact]
    public void DashboardDrillDownAndWorkspaceVisibilityAreDeterministic()
    {
        var root = new DesktopCompositionRoot();
        Assert.True(root.Workspace.IsDashboard);
        Assert.Equal(7, root.Workspace.Cards.Count);
        foreach (var area in root.Workspace.Areas)
        {
            root.Workspace.SelectArea(area);
            Assert.Equal(area, root.Workspace.SelectedArea);
            Assert.False(string.IsNullOrWhiteSpace(root.Workspace.Purpose));
        }
    }

    [Fact]
    public void OperationalSearchFiltersStructuredProjectionsAndProvidesEmptyState()
    {
        var root = new DesktopCompositionRoot();
        root.Workspace.SelectArea("SUBJECTS");
        root.Workspace.SearchText = "E002";
        Assert.Single(root.Workspace.FilteredSubjects);
        Assert.True(root.Workspace.HasSelection);
        root.Workspace.SearchText = "missing-subject";
        Assert.Empty(root.Workspace.FilteredSubjects);
        Assert.True(root.Workspace.IsEmpty);
        root.Workspace.ClearSearchCommand.Execute(null);
        Assert.False(root.Workspace.IsEmpty);
    }

    [Fact]
    public void ValidationKpiApprovalAndScenarioPresentationConsumesProjectedState()
    {
        var root = new DesktopCompositionRoot();
        root.Workspace.SelectArea("VALIDATE");
        root.Workspace.SetSeverityCommand.Execute("WARNING");
        Assert.Single(root.Workspace.FilteredFindings);
        root.Workspace.SelectArea("KPI");
        Assert.True(root.Workspace.CanCommitKpi);
        root.Workspace.SelectArea("APPROVAL");
        Assert.True(root.Workspace.CanApprove);
        Assert.False(root.Workspace.CanLock);
        root.Workspace.SelectArea("SCENARIO");
        Assert.Contains("NON-PRODUCTION", root.Workspace.ScenarioNotice);
    }

    [Fact]
    public void SettlementAccountingAndReportsPreservePinnedMeaning()
    {
        var root = new DesktopCompositionRoot();
        Assert.Contains(root.Workspace.Statutory, x => x.Status == "UNAVAILABLE" && x.Amount is null);
        Assert.Equal(root.Workspace.Accounting.Sum(x => x.Debit), root.Workspace.Accounting.Sum(x => x.Credit));
        Assert.All(root.Workspace.Reports, x => { Assert.Equal("1", x.Revision); Assert.Equal("DEMO-RUN-001", x.Run); });
        Assert.DoesNotContain(root.Workspace.Components, x => x.Code is "P1" or "P2" or "P3");
    }

    [Fact]
    public void Task20CSemanticIconsAreReplaceableAndLocalizedStateRemainsAvailable()
    {
        var provider = new SvgIconProvider();
        foreach (var key in Enum.GetValues<IconKey>()) Assert.Equal(key, provider.Resolve(key).Key);
        var root = new DesktopCompositionRoot();
        root.Culture.Select("vi-VN");
        root.Workspace.SelectArea("ACCOUNTING");
        Assert.Equal("HẠCH TOÁN", root.Workspace.AreaTitle);
        Assert.Contains("Tổng Nợ", root.Workspace.AccountingSummary);
    }
}
