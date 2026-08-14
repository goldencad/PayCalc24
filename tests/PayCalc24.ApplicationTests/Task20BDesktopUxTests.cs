using PayCalc24.Client.Avalonia;
using PayCalc24.Client.Avalonia.Presentation;
using PayCalc24.Contracts.Presentation;

namespace PayCalc24.ApplicationTests;

public sealed class Task20BDesktopUxTests
{
    [Fact]
    public void EveryOperationalRibbonCommandNavigatesTheWorkspace()
    {
        var root = new DesktopCompositionRoot();
        foreach (var area in root.Workspace.Areas)
        {
            Assert.True(root.Shell.NavigateAreaCommand.CanExecute(area));
            root.Shell.NavigateAreaCommand.Execute(area);
            Assert.Equal(area, root.Workspace.SelectedArea);
        }
    }

    [Fact]
    public void RuntimeLanguageChangesRibbonBackstageAndWorkspaceText()
    {
        var root = new DesktopCompositionRoot();
        root.Culture.Select("en-US");
        Assert.Equal("Home", root.Shell.TabHome);
        Assert.Contains("Company context", root.Workspace.CompanyBackstage);
        root.Culture.Select("vi-VN");
        Assert.Equal("Trang chủ", root.Shell.TabHome);
        Assert.Equal("TỔNG QUAN", root.Workspace.AreaTitle);
        Assert.Contains("Ngữ cảnh", root.Workspace.CompanyBackstage);
    }

    [Fact]
    public void RuntimeThemeOffersSystemLightAndDarkWithoutChangingPayrollData()
    {
        var root = new DesktopCompositionRoot();
        var componentValue = root.Workspace.Components[0].Value;
        foreach (var mode in Enum.GetValues<ThemeMode>())
        {
            root.Appearance.Select(mode);
            Assert.Equal(mode, root.Appearance.Mode);
            Assert.Equal(componentValue, root.Workspace.Components[0].Value);
        }
    }

    [Fact]
    public void DynamicComponentsAndUnavailableStatutoryRemainStructured()
    {
        var root = new DesktopCompositionRoot();
        Assert.All(root.Workspace.Components, row => Assert.False(string.IsNullOrWhiteSpace(row.Code)));
        Assert.Contains(root.Workspace.Statutory, row => row.Status == "UNAVAILABLE" && row.Amount is null);
        Assert.DoesNotContain(root.Workspace.Statutory, row => row.Status == "UNAVAILABLE" && row.Amount == 0m);
    }

    [Fact]
    public void SemanticIconCatalogCoversOperationalCommandsAndFallsBack()
    {
        var provider = new SvgIconProvider();
        foreach (var key in new[] { IconKey.Dashboard, IconKey.Subjects, IconKey.Inputs, IconKey.Attendance,
                     IconKey.Kpi, IconKey.Prepare, IconKey.Calculate, IconKey.Funds, IconKey.Validate,
                     IconKey.Explain, IconKey.Variance, IconKey.Scenario, IconKey.Approval,
                     IconKey.Settlement, IconKey.Accounting, IconKey.Reports, IconKey.Settings,
                     IconKey.Language, IconKey.Theme })
            Assert.NotEqual(IconKey.Missing, provider.Resolve(key).Key);
        Assert.Equal(IconKey.Missing, provider.Resolve((IconKey)int.MaxValue).Key);
    }
}
