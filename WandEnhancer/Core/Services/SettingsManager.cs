using System;
using System.IO;
using System.Text.Json;

namespace WandEnhancer.Core.Services;

public class AppSettings
{
    public string? Language { get; set; }
}

public static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        Constants.AppSettingsFileName);

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppSettings? LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            }
        }
        catch (Exception)
        {
            // Settings loading is non-critical - silently fall back to defaults
            // This can fail due to file permissions, corrupted JSON, etc.
        }
        return null;
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception)
        {
            // Settings saving is non-critical - silently ignore errors
            // This can fail due to file permissions or read-only directories
        }
    }
}