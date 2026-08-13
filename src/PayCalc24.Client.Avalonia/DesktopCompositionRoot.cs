using PayCalc24.Client.Avalonia.Features.Shell;
using PayCalc24.Client.Avalonia.Presentation;
using PayCalc24.Contracts.Identity;

namespace PayCalc24.Client.Avalonia;

public sealed class DesktopCompositionRoot
{
    public CultureState Culture { get; } = new();
    public AppearanceState Appearance { get; } = new();
    public NavigationService Navigation { get; } = new();
    // Placeholder is presentation-only until the authenticated TS24 company context is composed.
    public CompanyPresentationContext Company { get; } = new(CompanyId.From(Guid.Parse("00000000-0000-0000-0000-000000000001")));
    public PayrollWorkspaceState Payroll { get; }
    public ShellViewModel Shell { get; }
    public DesktopCompositionRoot()
    {
        Payroll = new PayrollWorkspaceState(Company);
        Shell = new ShellViewModel(Navigation, new DesktopLocalizationService(new DesktopResourceProvider()), Culture);
    }
}
