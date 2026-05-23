using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WandEnhancer.Core;
using WandEnhancer.Models;
using WandEnhancer.Utils;
using WandEnhancer.View.Popups;
using Application = System.Windows.Application;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace WandEnhancer.View.MainWindow;

public partial class MainWindowVm : ObservableObject
{
    private readonly MainWindow _view;
    public ObservableCollection<LogEntry> LogList { get; set; } = new ObservableCollection<LogEntry>();
    private static Updater _updater = new Updater();

    [ObservableProperty]
    public partial WeModConfig? WeModInfo { get; set; }

    [ObservableProperty]
    public partial bool IsPatchEnabled { get; set; }

    [ObservableProperty]
    public partial bool AlreadyPatched { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    partial void OnWeModInfoChanged(WeModConfig? value)
    {
        if (value == null) return;

        Log($"WeMod directory found at '{value}' ({value.ExecutableName})", ELogType.Success);
        if (File.Exists(Path.Combine(value.RootDirectory, "resources", "app.asar.backup")))
        {
            Log("WeMod already patched. If you want to patch again, please restore the backup first.",
                ELogType.Warn);
            IsPatchEnabled = false;
            AlreadyPatched = true;
            return;
        }

        Log("Ready for patching.", ELogType.Info);
        IsPatchEnabled = true;
    }

    [RelayCommand]
    private void SetFolderPath()
    {
        var dialog = new OpenFolderDialog
        {
            DefaultDirectory = Environment.GetEnvironmentVariable("LOCALAPPDATA"),
            Title = "Select the WeMod directory"
        };

        if (dialog.ShowDialog() is not true) return;
        var selectedPath = dialog.FolderName;
        var fileName = Path.GetFileName(selectedPath);

        var info = Extensions.CheckWeModPath(selectedPath);

        if (info != null)
        {
            WeModInfo = info;
            return;
        }

        LogList.Add(new LogEntry
        {
            LogType = ELogType.Error,
            Message = $"The selected folder '{fileName}' is not a valid WeMod directory."
        });
    }

    [RelayCommand]
    private void RestoreBackup()
    {
        if (WeModInfo == null)
        {
            Log("Can't be done. Please specify the directory first.", ELogType.Warn);
            return;
        }

        var backupPath = Path.Combine(WeModInfo.RootDirectory, "resources", "app.asar.backup");
        if (!File.Exists(backupPath))
        {
            Log("Backup not found. Please dont delete it manually", ELogType.Error);
            return;
        }

        try
        {
            // Try to lock the file to see if it's in use
            using (File.Open(backupPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }

            var proxyDllPath = Path.Combine(WeModInfo.RootDirectory, "version.dll");

            if (File.Exists(proxyDllPath))
            {
                File.Delete(proxyDllPath);
            }
        }
        catch
        {
            Log("Backup file is locked. Please close the WeMod and try again.", ELogType.Error);
            return;
        }

        File.Copy(backupPath, Path.Combine(WeModInfo.RootDirectory, "resources", "app.asar"), true);
        File.Delete(backupPath);
        Log("Backup restored successfully.", ELogType.Success);
        AlreadyPatched = false;
        IsPatchEnabled = true;
    }

    [RelayCommand]
    private void ApplyPatch()
    {
        var weMod = WeModInfo;
        if (weMod == null)
        {
            Log("Can't be done. Please specify the directory first.", ELogType.Warn);
            return;
        }

        MainWindow.Instance.OpenPopup(new PatchVectorsPopup(async config =>
        {
            MainWindow.Instance.ClosePopup();
            IsPatchEnabled = false;
            await Task.Run(() =>
            {
                try
                {
                    new Enhancer(weMod, Log, config).Patch();
                    AlreadyPatched = true;
                }
                catch (Exception e)
                {
                    Log($"Failed to patch: {e.Message}", ELogType.Error);
                    IsPatchEnabled = true;
                }
            });
        }), Application.Current.FindResource("pv_popup_title") as string);
    }

    private void Log(string message, ELogType logType)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            message = $"[{logType.ToString().ToUpper()}] {message}";

            var entry = new LogEntry
            {
                LogType = logType,
                Message = message
            };
            LogList.Add(entry);
            _view.LogList.ScrollIntoView(entry);
        });
    }

    [RelayCommand]
    private async Task Update()
    {
        var updateInfo = await _updater.GetUpdateInfoAsync();
        if (updateInfo == null)
        {
            Log("No update details are available right now.", ELogType.Warn);
            return;
        }

        MainWindow.Instance.OpenPopup(new UpdatePopup(Constants.Version?.ToString(), updateInfo.Version,
            updateInfo.LatestNotes, () =>
            {
                MainWindow.Instance.ClosePopup();
                Task.Run(async () =>
                {
                    try
                    {
                        await _updater.Update();
                    }
                    catch (Exception e)
                    {
                        Log($"Failed to update: {e.Message}", ELogType.Error);
                        return;
                    }

                    Log("WandEnhancer updated successfully. Restarting...", ELogType.Success);
                });
            }, () => _updater.GetFullChangelogAsync()), Application.Current.FindResource("up_popup_title") as string);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        MainWindow.Instance.OpenPopup(new SettingsPopup(), Application.Current.FindResource("settings_title") as string);
    }

    private string BuildLogReport()
    {
        var builder = new StringBuilder();
        foreach (var entry in LogList)
        {
            builder.AppendLine(entry.Message);
        }
        return builder.ToString();
    }

    [RelayCommand]
    private void CopyLogs()
    {
        if (LogList.Count == 0)
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(BuildLogReport());
            Log("Logs copied to clipboard.", ELogType.Success);
        }
        catch (Exception e)
        {
            Log($"Failed to copy logs: {e.Message}", ELogType.Error);
        }
    }

    [RelayCommand]
    private void ExportLogs()
    {
        if (LogList.Count == 0)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"wand-enhancer-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() is not true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, BuildLogReport());
            Log($"Logs exported to '{dialog.FileName}'.", ELogType.Success);
        }
        catch (Exception e)
        {
            Log($"Failed to export logs: {e.Message}", ELogType.Error);
        }
    }

    public MainWindowVm(MainWindow view)
    {
        Task.Run(async () =>
        {
            var isUpdateAvailable = await _updater.CheckForUpdates();
            Application.Current.Dispatcher.Invoke(() => IsUpdateAvailable = isUpdateAvailable);
        });
        _view = view;

        WeModInfo = Extensions.FindWeMod();
        if (WeModInfo == null)
        {
            Log("WeMod directory not found.", ELogType.Error);
        }
    }
}