using System.Collections.ObjectModel;
using System.Windows.Input;
using PayCalc24.Client.Avalonia.Presentation;
using PayCalc24.Contracts.Localization;

namespace PayCalc24.Client.Avalonia.Features.Shell;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly NavigationService navigation;
    private readonly ILocalizationService localization;
    private readonly CultureState culture;
    public ShellViewModel(NavigationService navigation, ILocalizationService localization, CultureState culture)
    {
        this.navigation = navigation; this.localization = localization; this.culture = culture;
        Items = new(new[] { AppRoute.Dashboard, AppRoute.Payroll, AppRoute.Configuration, AppRoute.Scenarios, AppRoute.Reports }
            .Select(route => new NavigationItem(route, $"Nav.{route}", route switch
            { AppRoute.Dashboard => IconKey.Dashboard, AppRoute.Payroll => IconKey.Payroll,
              AppRoute.Configuration => IconKey.Settings, AppRoute.Scenarios => IconKey.Scenario, _ => IconKey.Report })));
        NavigateCommand = new DelegateCommand(() => { });
        navigation.PropertyChanged += (_, _) => { Changed(nameof(CurrentTitle)); Changed(nameof(CurrentRoute)); };
        culture.CultureChanged += (_, _) => { Changed(nameof(AppTitle)); Changed(nameof(CurrentTitle)); Changed(nameof(StatusText)); Changed(nameof(LocalizedItems)); };
    }
    public ObservableCollection<NavigationItem> Items { get; }
    public IEnumerable<LocalizedNavigationItem> LocalizedItems => Items.Select(x =>
        new LocalizedNavigationItem(x.Route, localization.Resolve(x.ResourceKey, culture.Culture).Value));
    public AppRoute CurrentRoute => navigation.Current;
    public string AppTitle => localization.Resolve("App.Title", culture.Culture).Value;
    public string CurrentTitle => localization.Resolve($"Nav.{CurrentRoute}", culture.Culture).Value;
    public string StatusText => localization.Resolve("Shell.StatusReady", culture.Culture).Value;
    public ICommand NavigateCommand { get; }
    public void Navigate(AppRoute route) => navigation.Navigate(route);
}
public sealed record LocalizedNavigationItem(AppRoute Route, string Label);
