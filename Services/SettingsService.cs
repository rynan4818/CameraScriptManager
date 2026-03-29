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
    public string SongScriptsFolderPath { get; set; } = "";
    public string BackupRootPath { get; set; } = "";
    public bool AddMetadata { get; set; } = true;
    public bool EnableMapScriptsBackup { get; set; } = true;
    public bool EnableSongScriptsBackup { get; set; } = true;
    public bool EnableCopierBackup { get; set; } = true;
    public bool EnableAutoUpdateCheck { get; set; } = true;
    public DateTime? LastUpdateCheckUtc { get; set; }
    public string LastKnownLatestCameraScriptManagerVersion { get; set; } = "";
    public string DefaultRenameOption { get; set; } = "SongScript";

    // Naming settings
    public string ManagerZipNamingMode { get; set; } = "Default"; // Default or Custom
    public string ManagerZipCustomFormat { get; set; } = "{MapId}_{SongName}_{LevelAuthorName}";
    public string ManagerZipPackagingMode { get; set; } = "FolderKeepOriginalJson";
    public string CopierRenameNamingMode { get; set; } = "Default"; // Default or Custom
    public string CopierRenameCustomFormat { get; set; } = "{MapId}_{CameraScriptAuthorName}_{SongName}_SongScript";

    // Copier column visibility
    public bool ShowMetadataColumns { get; set; } = true;
    public Dictionary<string, Dictionary<string, double>> ColumnWidths { get; set; } = new();
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
