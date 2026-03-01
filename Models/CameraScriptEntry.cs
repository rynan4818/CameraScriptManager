namespace CameraScriptManager.Models;

public class CameraScriptEntry
{
    public string MapId { get; set; } = "";
    public string CameraScriptAuthorName { get; set; } = "";
    public string SongName { get; set; } = "";
    public string SongSubName { get; set; } = "";
    public string SongAuthorName { get; set; } = "";
    public string LevelAuthorName { get; set; } = "";
    public double Bpm { get; set; }
    public double Duration { get; set; }
    public string FileName { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string FolderName { get; set; } = "";
    public string SourceType { get; set; } = ""; // "CustomLevels" or "CustomWIPLevels"
    public string FullFilePath { get; set; } = "";
    public string JsonContent { get; set; } = "";
    public bool HasOriginalMetadata { get; set; }
    public bool IsCameraScriptAuthorFromMetadata { get; set; }
    public double ScriptDuration { get; set; }
    public double OggDuration { get; set; }
    public List<string> OriginalSourceFiles { get; set; } = new();
}
