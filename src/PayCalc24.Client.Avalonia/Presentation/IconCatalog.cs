namespace PayCalc24.Client.Avalonia.Presentation;

public enum IconKey { Dashboard, Subjects, Inputs, Attendance, Kpi, Prepare, Calculate, Funds, Validate, Explain, Variance, Scenario, Approval, Settlement, Accounting, Reports, Settings, Language, Theme, Payroll, Employee, Review, Approve, Lock, Report, StatusInfo, StatusWarning, StatusError, Missing }
public sealed record IconDescriptor(IconKey Key, Uri AssetUri);
public interface IIconProvider { IconDescriptor Resolve(IconKey key); }

public sealed class SvgIconProvider : IIconProvider
{
    private static readonly Dictionary<IconKey, string> Assets = new()
    {
        [IconKey.Dashboard]="dashboard", [IconKey.Subjects]="person", [IconKey.Inputs]="action", [IconKey.Attendance]="action", [IconKey.Kpi]="action",
        [IconKey.Prepare]="payroll", [IconKey.Funds]="payroll", [IconKey.Validate]="status", [IconKey.Explain]="action", [IconKey.Variance]="action",
        [IconKey.Approval]="action", [IconKey.Settlement]="payroll", [IconKey.Accounting]="report", [IconKey.Reports]="report",
        [IconKey.Language]="action", [IconKey.Theme]="action", [IconKey.Payroll]="payroll", [IconKey.Employee]="person",
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
