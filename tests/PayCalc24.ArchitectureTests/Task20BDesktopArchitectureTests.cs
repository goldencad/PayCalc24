namespace PayCalc24.ArchitectureTests;

public sealed class Task20BDesktopArchitectureTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void MainWindowUsesActiproRibbonBackstageAndStructuredTemplates()
    {
        var xaml = File.ReadAllText(Path.Combine(Root, "src", "PayCalc24.Client.Avalonia", "MainWindow.axaml"));
        Assert.Contains("<actipro:Ribbon x:Name=\"MainRibbon\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<actipro:RibbonBackstage x:Name=\"MainBackstage\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EnglishOption\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"VietnameseOption\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SystemThemeOption\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"LightThemeOption\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DarkThemeOption\"", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding Code}", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ListBox ItemsSource=\"{Binding Workspace.Subjects}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ListBox ItemsSource=\"{Binding Workspace.Components}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopHasNoBusinessEngineDatabaseOrFixedComponentPresentation()
    {
        var directory = Path.Combine(Root, "src", "PayCalc24.Client.Avalonia");
        var source = string.Join('\n', Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".axaml", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        foreach (var forbidden in new[] { "MariaDb", "DbContext", "P1Calculator", "P2Calculator", "P3Calculator", "SalesCalculator", "OfficeCalculator", "SubjectRow {", "ComponentRow {", "FundRow {" })
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "PayCalc24.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
