using System.Collections.ObjectModel;
using System.Windows.Input;
using PayCalc24.Client.Avalonia.Presentation;
using PayCalc24.Contracts.Localization;
using PayCalc24.Client.Avalonia.Features.Payroll;

namespace PayCalc24.Client.Avalonia.Features.Shell;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly NavigationService navigation;
    private readonly ILocalizationService localization;
    private readonly CultureState culture;
    public ShellViewModel(NavigationService navigation, ILocalizationService localization, CultureState culture,
        AppearanceState appearance, OperationalWorkspaceViewModel workspace)
    {
        this.navigation = navigation; this.localization = localization; this.culture = culture;
        Appearance = appearance; Workspace = workspace;
        Items = new(new[] { AppRoute.Dashboard, AppRoute.Payroll, AppRoute.Configuration, AppRoute.Scenarios, AppRoute.Reports }
            .Select(route => new NavigationItem(route, $"Nav.{route}", route switch
            { AppRoute.Dashboard => IconKey.Dashboard, AppRoute.Payroll => IconKey.Payroll,
              AppRoute.Configuration => IconKey.Settings, AppRoute.Scenarios => IconKey.Scenario, _ => IconKey.Report })));
        NavigateCommand = new DelegateCommand(() => { });
        ExitCommand = new DelegateCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
        navigation.PropertyChanged += (_, _) => { Changed(nameof(CurrentTitle)); Changed(nameof(CurrentRoute)); };
        culture.CultureChanged += (_, _) => { Changed(nameof(AppTitle)); Changed(nameof(CurrentTitle)); Changed(nameof(StatusText)); Changed(nameof(LocalizedItems)); Changed(string.Empty); };
    }
    public ObservableCollection<NavigationItem> Items { get; }
    public IEnumerable<LocalizedNavigationItem> LocalizedItems => Items.Select(x =>
        new LocalizedNavigationItem(x.Route, localization.Resolve(x.ResourceKey, culture.Culture).Value));
    public AppRoute CurrentRoute => navigation.Current;
    public string AppTitle => localization.Resolve("App.Title", culture.Culture).Value;
    public string CurrentTitle => localization.Resolve($"Nav.{CurrentRoute}", culture.Culture).Value;
    public string StatusText => localization.Resolve("Shell.StatusReady", culture.Culture).Value;
    public ICommand NavigateCommand { get; }
    public ICommand ExitCommand { get; }
    public event EventHandler? ExitRequested;
    public AppearanceState Appearance { get; }
    public OperationalWorkspaceViewModel Workspace { get; }
    public ICommand EnglishCommand => new DelegateCommand(() => culture.Select("en-US"));
    public ICommand VietnameseCommand => new DelegateCommand(() => culture.Select("vi-VN"));
    public ICommand LightCommand => new DelegateCommand(() => Appearance.Select(PayCalc24.Contracts.Presentation.ThemeMode.LIGHT));
    public ICommand DarkCommand => new DelegateCommand(() => Appearance.Select(PayCalc24.Contracts.Presentation.ThemeMode.DARK));
    public ICommand SystemCommand => new DelegateCommand(() => Appearance.Select(PayCalc24.Contracts.Presentation.ThemeMode.SYSTEM));
    public ICommand NavigateAreaCommand => Workspace.SelectAreaCommand;
    public string TabHome => Local("Home", "Trang chủ");
    public string TabInput => Local("Input", "Đầu vào");
    public string TabPayroll => Local("Payroll", "Tính lương");
    public string TabReview => Local("Review", "Rà soát");
    public string TabApproval => Local("Approval", "Phê duyệt");
    public string TabFinance => Local("Finance", "Tài chính");
    public string TabReports => Local("Reports", "Báo cáo");
    public string DashboardLabel => Local("Dashboard", "Tổng quan");
    public string SubjectsLabel => Local("Subjects", "Nhân sự");
    public string InputsLabel => Local("Payroll Inputs", "Dữ liệu lương");
    public string AttendanceLabel => Local("Attendance", "Chấm công");
    public string PrepareLabel => Local("Prepare", "Chuẩn bị");
    public string CalculateLabel => Local("Calculate", "Tính lương");
    public string FundsLabel => Local("Funds", "Nguồn quỹ");
    public string ValidateLabel => Local("Validate", "Kiểm tra");
    public string ExplainLabel => Local("Explain", "Giải trình");
    public string VarianceLabel => Local("Variance", "Biến động");
    public string ScenarioLabel => Local("Scenario", "Kịch bản");
    public string SettlementLabel => Local("Settlement", "Quyết toán");
    public string AccountingLabel => Local("Accounting", "Hạch toán");
    public string FileKeyTip => Local("F", "F");
    public string BackstageApplication => Local("Application", "Ứng dụng");
    public string BackstageSettings => Local("Settings", "Cài đặt");
    public string BackstageAbout => Local("About", "Giới thiệu");
    public string BackstageExit => Local("Exit", "Thoát");
    public string LanguageHeading => Local("Language", "Ngôn ngữ");
    public string EnglishLabel => Local("English (United States)", "English (United States)");
    public string VietnameseLabel => Local("Tiếng Việt", "Tiếng Việt");
    public string ThemeHeading => Local("Theme", "Giao diện");
    public string SystemThemeLabel => Local("System", "Hệ thống");
    public string LightThemeLabel => Local("Light", "Sáng");
    public string DarkThemeLabel => Local("Dark", "Tối");
    public string AboutTitle => Local("About PayCalc24", "Giới thiệu PayCalc24");
    public string AboutDescription => Local("Desktop operational MVP · Presentation-only demo adapter", "MVP desktop vận hành · Bộ chuyển đổi demo chỉ dành cho trình bày");
    public string ExitLabel => Local("Exit PayCalc24", "Thoát PayCalc24");
    public string RunActionLabel => Local("Run action", "Thực hiện");
    public string InputSectionLabel => Local("Payroll inputs · Attendance · KPI", "Dữ liệu lương · Chấm công · KPI");
    public string ReviewSectionLabel => Local("Variance · Approval · Accounting · Reports", "Biến động · Phê duyệt · Hạch toán · Báo cáo");
    public string ViewLabel => Local("View", "Xem");
    private string Local(string english, string vietnamese) => culture.Culture == "vi-VN" ? vietnamese : english;
    public void Navigate(AppRoute route) => navigation.Navigate(route);
}
public sealed record LocalizedNavigationItem(AppRoute Route, string Label);
