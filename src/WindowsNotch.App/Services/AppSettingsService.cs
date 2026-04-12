using System.IO;
using System.Text.Json;
using WindowsNotch.App.Models;

namespace WindowsNotch.App.Services;

public sealed class AppSettingsService
{
    private readonly string _settingsPath;

    public AppSettingsService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsNotch",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var settingsDirectory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("Settings directory could not be resolved.");

        Directory.CreateDirectory(settingsDirectory);

        var json = JsonSerializer.Serialize(
            settings,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });

        File.WriteAllText(_settingsPath, json);
    }
}
