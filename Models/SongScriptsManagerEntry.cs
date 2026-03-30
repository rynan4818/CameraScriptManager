namespace CameraScriptManager.Models;

public class SongScriptsManagerEntry
{
    public string SourceFilePath { get; set; } = "";
    public string SourceRelativePath { get; set; } = "";
    public string? ZipEntryName { get; set; }
    public string SourceDisplayPath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string JsonContent { get; set; } = "";
    public bool HasMetadataBlock { get; set; }
    public string MetadataMapId { get; set; } = "";
    public string PathMapId { get; set; } = "";
    public string MapId { get; set; } = "";
    public string Hash { get; set; } = "";
    public string CameraScriptAuthorName { get; set; } = "";
    public string SongName { get; set; } = "";
    public string SongSubName { get; set; } = "";
    public string SongAuthorName { get; set; } = "";
    public string LevelAuthorName { get; set; } = "";
    public double Bpm { get; set; }
    public double Duration { get; set; }
    public double? AvatarHeight { get; set; }
    public string Description { get; set; } = "";
    public List<SongScriptsMatchedBeatmapFolder> MatchedCustomLevels { get; set; } = new();
    public List<SongScriptsMatchedBeatmapFolder> MatchedCustomWIPLevels { get; set; } = new();
    public string? MissingBeatmapMapId { get; set; }

    public bool IsZipEntry => !string.IsNullOrEmpty(ZipEntryName);
}
