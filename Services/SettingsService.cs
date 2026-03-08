using System.IO;
using System.Text.Json;

namespace CameraScriptManager.Services;

public class AppSettings
{
    public string CustomLevelsPath { get; set; } = "";
    public string CustomWIPLevelsPath { get; set; } = "";
    public string OriginalScriptPath1 { get; set; } = "";
    public string OriginalScriptPath2 { get; set; } = "";
    public string OriginalScriptPath3 { get; set; } = "";
    public bool AddMetadata { get; set; } = true;
    public bool CreateBackup { get; set; } = true;
    public string DefaultRenameOption { get; set; } = "SongScript";
    public bool? DefaultRenameToAuthorIdSongName { get; set; }

    // Naming settings
    public string ManagerZipNamingMode { get; set; } = "Default"; // Default or Custom
    public string ManagerZipCustomFormat { get; set; } = "{MapId}_{SongName}_{LevelAuthorName}";
    public string CopierRenameNamingMode { get; set; } = "Default"; // Default or Custom
    public string CopierRenameCustomFormat { get; set; } = "{CameraScriptAuthorName}_{MapId}_{SongName}_SongScript";

    // Copier column visibility
    public bool ShowMetadataColumns { get; set; } = true;
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
