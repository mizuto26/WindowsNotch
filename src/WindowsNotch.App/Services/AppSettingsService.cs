using System.IO;
using System.Text.Json;
using WindowsNotch.App.Models;

namespace WindowsNotch.App.Services;

public sealed class AppSettingsService
{
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(AppPaths.SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var settingsDirectory = Path.GetDirectoryName(AppPaths.SettingsFilePath)
            ?? throw new InvalidOperationException("Settings directory could not be resolved.");

        Directory.CreateDirectory(settingsDirectory);

        var json = JsonSerializer.Serialize(
            settings,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });

        File.WriteAllText(AppPaths.SettingsFilePath, json);
    }
}
