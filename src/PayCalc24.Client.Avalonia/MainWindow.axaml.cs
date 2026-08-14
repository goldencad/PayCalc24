using Avalonia.Controls;
using Avalonia.Interactivity;
using PayCalc24.Client.Avalonia.Features.Shell;
using PayCalc24.Client.Avalonia.Presentation;
using PayCalc24.Contracts.Presentation;

namespace PayCalc24.Client.Avalonia;

public sealed partial class MainWindow : ActiproSoftware.UI.Avalonia.Controls.Bars.RibbonWindow
{
    public MainWindow() : this(new DesktopCompositionRoot()) { }
    public MainWindow(DesktopCompositionRoot root)
    {
        InitializeComponent();
        DataContext = root.Shell;
    }
    private void OnNavigationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ShellViewModel shell && sender is ListBox { SelectedItem: LocalizedNavigationItem item })
            shell.Navigate(item.Route);
    }
    private void OnWorkspaceAreaChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ShellViewModel shell && sender is ListBox { SelectedItem: string area })
            shell.Workspace.SelectArea(area);
    }
    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    internal IReadOnlyList<string> RunSmokeChecks(DesktopCompositionRoot root)
    {
        var checks = new List<string>();
        if (MainRibbon is null) throw new InvalidOperationException("SMOKE.ACTIPRO_RIBBON_MISSING");
        checks.Add("Actipro Ribbon renders");
        MainBackstage.IsOpen = true;
        if (!MainBackstage.IsOpen) throw new InvalidOperationException("SMOKE.ACTIPRO_BACKSTAGE_DID_NOT_OPEN");
        checks.Add("Actipro Backstage opens");
        MainBackstage.IsOpen = false;
        root.Workspace.SelectArea("SUBJECTS"); root.Workspace.SelectArea("DASHBOARD");
        checks.Add("Navigation and dashboard/workspace load");
        root.Culture.Select("vi-VN"); root.Culture.Select("en-US");
        checks.Add("en-US and vi-VN resources load");
        root.Appearance.Select(ThemeMode.LIGHT); root.Appearance.Select(ThemeMode.DARK); root.Appearance.Select(ThemeMode.SYSTEM);
        checks.Add("Light, Dark, and System themes initialize");
        var icons = new SvgIconProvider();
        foreach (var key in Enum.GetValues<IconKey>()) _ = icons.Resolve(key);
        checks.Add("SVG IconKey assets resolve");
        if (!root.Workspace.DemoNotice.Contains("NON-PRODUCTION", StringComparison.Ordinal))
            throw new InvalidOperationException("SMOKE.DEMO_MARKER_MISSING");
        checks.Add("Demo data is marked non-production");
        if (!root.Workspace.Statutory.Any(x => x.Status == "UNAVAILABLE" && x.Amount is null))
            throw new InvalidOperationException("SMOKE.MISSING_STATUTORY_NOT_EXPLICIT");
        checks.Add("Missing statutory value displays UNAVAILABLE");
        if (!root.Workspace.RevisionIdentity.Contains("snapshot 1", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SMOKE.REVISION_IDENTITY_MISSING");
        checks.Add("Historical revision identity remains pinned");
        return checks;
    }
}
