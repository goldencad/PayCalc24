using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Localization;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.Presentation;

namespace PayCalc24.Client.Avalonia.Presentation;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class DelegateCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class DelegateCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => execute((T?)parameter);
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncDelegateCommand(Func<Task> execute) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running;
    public async void Execute(object? parameter)
    {
        if (running) return;
        running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute().ConfigureAwait(true); }
        finally { running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}

public enum AppRoute { Dashboard, Payroll, Configuration, Scenarios, Reports }
public sealed record NavigationItem(AppRoute Route, string ResourceKey, IconKey Icon);

public sealed class NavigationService : ViewModelBase
{
    private AppRoute current = AppRoute.Dashboard;
    public AppRoute Current { get => current; private set { if (current == value) return; current = value; Changed(); } }
    public void Navigate(AppRoute route) => Current = route;
}

public sealed class CompanyPresentationContext : ViewModelBase
{
    private CompanyId companyId;
    public CompanyPresentationContext(CompanyId initial) => companyId = initial;
    public CompanyId CompanyId { get => companyId; private set { companyId = value; Changed(); } }
    public event EventHandler? CompanyChanged;
    public void Switch(CompanyId company)
    {
        if (company == CompanyId) return;
        CompanyId = company;
        CompanyChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record PayrollRevisionContext(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    DateOnly BusinessDate, PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision,
    PayrollCalculationRunId CalculationRunId, string ApprovalState);

public sealed class PayrollWorkspaceState : ViewModelBase
{
    private readonly CompanyPresentationContext companies;
    private PayrollRevisionContext? revision;
    private PayrollSubjectId? selectedSubject;
    public PayrollWorkspaceState(CompanyPresentationContext companies)
    {
        this.companies = companies;
        companies.CompanyChanged += (_, _) => Clear();
    }
    public PayrollRevisionContext? Revision { get => revision; private set { revision = value; Changed(); } }
    public PayrollSubjectId? SelectedSubject { get => selectedSubject; private set { selectedSubject = value; Changed(); } }
    public void Open(PayrollRevisionContext value)
    {
        if (value.CompanyId != companies.CompanyId) throw new InvalidOperationException("PRESENTATION.COMPANY_CONTEXT_MISMATCH");
        Revision = value;
        SelectedSubject = null;
    }
    public void Select(PayrollSubjectId subject) => SelectedSubject = subject;
    private void Clear() { Revision = null; SelectedSubject = null; }
}

public sealed class CultureState : ViewModelBase
{
    private string culture = SupportedCultures.Default;
    public string Culture { get => culture; private set { culture = value; Changed(); Changed(nameof(CultureInfo)); } }
    public CultureInfo CultureInfo => CultureInfo.GetCultureInfo(Culture);
    public event EventHandler? CultureChanged;
    public void Select(string value)
    {
        if (!SupportedCultures.All.Contains(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(value));
        var canonical = SupportedCultures.All.Single(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
        if (canonical == Culture) return;
        Culture = canonical;
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class AppearanceState : ViewModelBase
{
    private ThemeMode mode = ThemeMode.SYSTEM;
    public ThemeMode Mode { get => mode; private set { mode = value; Changed(); ThemeChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler? ThemeChanged;
    public void Select(ThemeMode value) => Mode = value;
}

public sealed class DesktopResourceProvider : ILocalizationResourceProvider
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Values =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [SupportedCultures.English] = new Dictionary<string, string>
            {
                ["App.Title"]="PayCalc24", ["Nav.Dashboard"]="Dashboard", ["Nav.Payroll"]="Payroll",
                ["Nav.Configuration"]="Configuration", ["Nav.Scenarios"]="Scenarios", ["Nav.Reports"]="Reports",
                ["Shell.Foundation"]="Presentation foundation", ["Shell.StatusReady"]="Ready",
                ["Diagnostic.Unknown"]="Diagnostic {0}"
            },
            [SupportedCultures.Vietnamese] = new Dictionary<string, string>
            {
                ["App.Title"]="PayCalc24", ["Nav.Dashboard"]="Tổng quan", ["Nav.Payroll"]="Bảng lương",
                ["Nav.Configuration"]="Cấu hình", ["Nav.Scenarios"]="Kịch bản", ["Nav.Reports"]="Báo cáo",
                ["Shell.Foundation"]="Nền tảng trình bày", ["Shell.StatusReady"]="Sẵn sàng",
                ["Diagnostic.Unknown"]="Chẩn đoán {0}"
            }
        };
    public bool TryGet(string culture, string resourceKey, out string value)
    {
        if (Values.TryGetValue(culture, out var resources) && resources.TryGetValue(resourceKey, out var found))
        {
            value = found;
            return true;
        }
        value = string.Empty;
        return false;
    }
}

public sealed class DesktopLocalizationService(ILocalizationResourceProvider resources) : ILocalizationService
{
    public LocalizedResource Resolve(string resourceKey, string? preferredCulture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        var culture = SupportedCultures.All.Contains(preferredCulture, StringComparer.OrdinalIgnoreCase)
            ? SupportedCultures.All.Single(x => string.Equals(x, preferredCulture, StringComparison.OrdinalIgnoreCase))
            : SupportedCultures.Default;
        if (resources.TryGet(culture, resourceKey, out var value)) return new(resourceKey, value, culture, null);
        if (resources.TryGet(SupportedCultures.Default, resourceKey, out value)) return new(resourceKey, value, SupportedCultures.Default, null);
        return new(resourceKey, resourceKey, SupportedCultures.Default,
            new Diagnostic("LOCALIZATION.RESOURCE_MISSING", DiagnosticSeverity.Warning,
                new Dictionary<string, object?> { ["resourceKey"] = resourceKey }));
    }
}

public sealed record DiagnosticPresentation(string Code, DiagnosticSeverity Severity, string Message, IconKey Icon);
public sealed class DiagnosticPresenter(ILocalizationService localization)
{
    public DiagnosticPresentation Present(Diagnostic diagnostic, string culture)
    {
        var resolved = localization.Resolve($"Diagnostic.{diagnostic.Code}", culture);
        var message = resolved.Diagnostic is null ? resolved.Value
            : string.Format(CultureInfo.GetCultureInfo(culture), localization.Resolve("Diagnostic.Unknown", culture).Value, diagnostic.Code);
        var icon = diagnostic.Severity switch { DiagnosticSeverity.Info => IconKey.StatusInfo,
            DiagnosticSeverity.Warning => IconKey.StatusWarning, _ => IconKey.StatusError };
        return new(diagnostic.Code, diagnostic.Severity, message, icon);
    }
}
