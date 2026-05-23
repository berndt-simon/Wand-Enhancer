using System.Windows;
using System.Windows.Input;

namespace WandEnhancer.View.MainWindow;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public static MainWindow Instance = null!;
    public readonly MainWindowVm ViewModel;

    public MainWindow()
    {
        InitializeComponent();
        this.ViewModel = new MainWindowVm(this);
        this.DataContext = ViewModel;
        VersionLabel.Text = Constants.Version?.ToString() ?? string.Empty;
        Instance = this;

    }

    public void OpenPopup(FrameworkElement content, string? title = null)
    {
        this.PopupHost.PopupContent = content;
        PopupHost.Title.Text = title ?? string.Empty;
        PopupHost.IsOpen = true;
    }

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        this.DragMove();
    }

    private void OnClosing(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    public void ClosePopup()
    {
        PopupHost.IsOpen = false;
    }

    private void OpenSourceClicked(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(Constants.RepositoryUrl);
    }
}