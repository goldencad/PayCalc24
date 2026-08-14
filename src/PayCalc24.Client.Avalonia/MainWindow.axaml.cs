using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
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
        RibbonHost.ZIndex = 1;
        DataContext = root.Shell;
        root.Shell.ExitRequested += (_, _) => Close();
    }
    private void OnNavigationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ShellViewModel shell && sender is ListBox { SelectedItem: LocalizedNavigationItem item })
            shell.Navigate(item.Route);
    }
    internal IReadOnlyList<string> RunSmokeChecks(DesktopCompositionRoot root)
    {
        var checks = new List<string>();
        if (MainRibbon is null) throw new InvalidOperationException("SMOKE.ACTIPRO_RIBBON_MISSING");
        checks.Add("Actipro Ribbon renders");
        if (MainBackstage is null) throw new InvalidOperationException("SMOKE.ACTIPRO_BACKSTAGE_MISSING");
        checks.Add("Actipro Backstage is integrated (open verified by manual GUI smoke)");
        foreach (var area in new[] { "DASHBOARD", "INPUTS", "KPI", "CALCULATE", "EXPLAIN", "APPROVAL", "ACCOUNTING", "REPORTS" })
        {
            root.Workspace.SelectAreaCommand.Execute(area);
            if (root.Workspace.SelectedArea != area) throw new InvalidOperationException($"SMOKE.RIBBON_NAVIGATION_FAILED:{area}");
        }
        root.Workspace.SelectAreaCommand.Execute("DASHBOARD");
        checks.Add("Ribbon navigation and dashboard/workspace load");
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

    internal void CaptureSmokeEvidence(string path)
    {
        var size = new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(this);
        bitmap.Save(path);
    }
}
