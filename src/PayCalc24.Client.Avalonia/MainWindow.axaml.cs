using Avalonia.Controls;
using Avalonia.Interactivity;
using PayCalc24.Client.Avalonia.Features.Shell;

namespace PayCalc24.Client.Avalonia;

public sealed partial class MainWindow : Window
{
    public MainWindow() : this(new DesktopCompositionRoot()) { }
    public MainWindow(DesktopCompositionRoot root)
    {
        InitializeComponent();
        DataContext = root.Shell;
    }
    private void OnNavigationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ShellViewModel shell && sender is ListBox { SelectedItem: LocalizedNavigationItem item })
            shell.Navigate(item.Route);
    }
}
