using System.Windows;
using WandEnhancer.Core.Services;

namespace WandEnhancer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LocalizationManager.Initialize();
        this.MainWindow.Show();
    }

    public new static void Shutdown()
    {
        Current.Dispatcher.Invoke(() => Current.Shutdown());
    }
}