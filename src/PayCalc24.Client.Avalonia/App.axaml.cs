using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using PayCalc24.Contracts.Presentation;

namespace PayCalc24.Client.Avalonia;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var root = new DesktopCompositionRoot();
            ApplyTheme(root.Appearance.Mode);
            root.Appearance.ThemeChanged += (_, _) => ApplyTheme(root.Appearance.Mode);
            desktop.MainWindow = new MainWindow(root);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyTheme(ThemeMode mode) => RequestedThemeVariant = mode switch
    {
        ThemeMode.LIGHT => ThemeVariant.Light,
        ThemeMode.DARK => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };
}
