namespace PayCalc24.ArchitectureTests;

public sealed class Task20CDesktopBoundaryTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void DesktopWorkspacesArePresentationOnlyAndDoNotEncodeFixedComponents()
    {
        var directory = Path.Combine(Root, "src", "PayCalc24.Client.Avalonia");
        var source = string.Join('\n', Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(x => x.EndsWith(".cs", StringComparison.Ordinal) || x.EndsWith(".axaml", StringComparison.Ordinal)).Select(File.ReadAllText));
        foreach (var forbidden in new[] { "Infrastructure.MariaDb", "DbContext", "FormulaEvaluator", "FundAllocator", "SalesPayrollView", "OfficePayrollView", "P1View", "P2View", "P3View" })
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding Workspace.IsDashboard}\"", source, StringComparison.Ordinal);
        Assert.Contains("UNAVAILABLE is not zero", source, StringComparison.Ordinal);
        Assert.Contains("CanApprove", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RibbonExposesContextActionsAndEveryMajorStateHasPresentation()
    {
        var xaml = File.ReadAllText(Path.Combine(Root, "src", "PayCalc24.Client.Avalonia", "MainWindow.axaml"));
        foreach (var command in new[] { "RefreshDashboard", "ValidateKpi", "CommitKpi", "Approve", "Reject", "Lock", "Generate", "Preview", "Export" })
            Assert.Contains($"Key=\"{command}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Workspace.Busy", xaml, StringComparison.Ordinal);
        Assert.Contains("Workspace.HasError", xaml, StringComparison.Ordinal);
        Assert.Contains("Workspace.IsEmpty", xaml, StringComparison.Ordinal);
        Assert.Contains("Workspace.ReadOnlyReason", xaml, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "PayCalc24.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
