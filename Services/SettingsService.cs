using System.IO;
using System.Text.Json;

namespace CameraScriptManager.Services;

public class AppSettings
{
    public string CustomLevelsPath { get; set; } = "";
    public string CustomWIPLevelsPath { get; set; } = "";
}

public class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        _settingsPath = Path.Combine(exeDir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }
}
