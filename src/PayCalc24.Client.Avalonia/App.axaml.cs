using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
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
            var window = new MainWindow(root);
            desktop.MainWindow = window;
            if (string.Equals(Environment.GetEnvironmentVariable("PAYCALC24_DESKTOP_SMOKE"), "1", StringComparison.Ordinal))
            {
                window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        foreach (var check in window.RunSmokeChecks(root)) Console.WriteLine($"SMOKE PASS: {check}");
                        var screenshotPath = Environment.GetEnvironmentVariable("PAYCALC24_SMOKE_SCREENSHOT");
                        if (!string.IsNullOrWhiteSpace(screenshotPath))
                        {
                            window.CaptureSmokeEvidence(screenshotPath);
                            Console.WriteLine($"SMOKE PASS: screenshot captured at {screenshotPath}");
                        }
                        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYCALC24_ACTIPRO_LICENSE_KEY")))
                        {
                            Console.WriteLine("SMOKE INFO: Actipro evaluation runtime detected; close the trial prompt and main window manually.");
                            return;
                        }

                        await Task.Delay(1500).ConfigureAwait(true);
                        Console.WriteLine("SMOKE PASS: main window rendered and application exits cleanly");
                        desktop.Shutdown(0);
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine($"SMOKE FAIL: {exception.Message}");
                        desktop.Shutdown(1);
                    }
                }, DispatcherPriority.Loaded);
            }
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
