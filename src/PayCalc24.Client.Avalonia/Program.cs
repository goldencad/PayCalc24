using Avalonia;

namespace PayCalc24.Client.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            var crashLogPath = Environment.GetEnvironmentVariable("PAYCALC24_CRASH_LOG");
            if (!string.IsNullOrWhiteSpace(crashLogPath))
                File.WriteAllText(crashLogPath, exception.ToString());
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>();
        var licensee = Environment.GetEnvironmentVariable("PAYCALC24_ACTIPRO_LICENSEE");
        var licenseKey = Environment.GetEnvironmentVariable("PAYCALC24_ACTIPRO_LICENSE_KEY");
        if (!string.IsNullOrWhiteSpace(licensee) && !string.IsNullOrWhiteSpace(licenseKey))
            builder = builder.RegisterActiproLicense(licensee, licenseKey);
        return builder.UsePlatformDetect().LogToTrace();
    }
}
