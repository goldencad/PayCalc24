namespace PayCalc24.Client.Avalonia.Presentation;

public enum IconKey { Dashboard, Payroll, Employee, Calculate, Review, Approve, Lock, Scenario, Report, Settings, StatusInfo, StatusWarning, StatusError, Missing }
public sealed record IconDescriptor(IconKey Key, Uri AssetUri);
public interface IIconProvider { IconDescriptor Resolve(IconKey key); }

public sealed class SvgIconProvider : IIconProvider
{
    private static readonly Dictionary<IconKey, string> Assets = new()
    {
        [IconKey.Dashboard]="dashboard", [IconKey.Payroll]="payroll", [IconKey.Employee]="person",
        [IconKey.Calculate]="action", [IconKey.Review]="action", [IconKey.Approve]="action",
        [IconKey.Lock]="action", [IconKey.Scenario]="dashboard", [IconKey.Report]="report",
        [IconKey.Settings]="action", [IconKey.StatusInfo]="status", [IconKey.StatusWarning]="status",
        [IconKey.StatusError]="status", [IconKey.Missing]="missing"
    };
    public IconDescriptor Resolve(IconKey key)
    {
        if (!Assets.TryGetValue(key, out var asset)) { key = IconKey.Missing; asset = Assets[key]; }
        return new(key, new Uri($"avares://PayCalc24.Client.Avalonia/Assets/Icons/{asset}.svg"));
    }
}
