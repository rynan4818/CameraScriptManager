namespace CameraScriptManager.Models;

public sealed class SearchCacheDocument
{
    public const int CurrentVersion = 6;

    public int Version { get; set; } = CurrentVersion;
    public Dictionary<string, CachedSongScriptsSourceResult> SongScriptsSources { get; set; } = new();
    public Dictionary<string, CachedBeatmapFolderResult> BeatmapFolders { get; set; } = new();
    public Dictionary<string, CachedCameraScriptScanEntry> CameraScriptEntries { get; set; } = new();
    public CachedOriginalScriptMatchResult OriginalScriptMatch { get; set; } = new();
}

public sealed class SearchCacheFileStamp
{
    public string Path { get; set; } = "";
    public long Length { get; set; }
    public DateTime CreationTimeUtc { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
}

public sealed class CachedSongScriptsSourceResult
{
    public SearchCacheFileStamp SourceFile { get; set; } = new();
    public List<CachedSongScriptsEntry> Entries { get; set; } = new();
}

public sealed class CachedSongScriptsEntry
{
    public string SourceFilePath { get; set; } = "";
    public string SourceRelativePath { get; set; } = "";
    public string? ZipEntryName { get; set; }
    public string SourceDisplayPath { get; set; } = "";
    public string FileName { get; set; } = "";
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
}

public sealed class CachedBeatmapFolderResult
{
    public string FullPath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string FolderName { get; set; } = "";
    public bool IsCustomLevels { get; set; }
    public bool IsCompatibleBeatmap { get; set; }
    public string MapId { get; set; } = "";
    public string Hash { get; set; } = "";
    public List<SearchCacheFileStamp> FileStamps { get; set; } = new();
}

public sealed class CachedCameraScriptScanEntry
{
    public SearchCacheFileStamp SourceFile { get; set; } = new();
    public List<SearchCacheFileStamp> FolderFiles { get; set; } = new();
    public CachedCameraScriptEntry Entry { get; set; } = new();
}

public sealed class CachedCameraScriptEntry
{
    public string MapId { get; set; } = "";
    public string CameraScriptAuthorName { get; set; } = "";
    public string SongName { get; set; } = "";
    public string SongSubName { get; set; } = "";
    public string SongAuthorName { get; set; } = "";
    public string LevelAuthorName { get; set; } = "";
    public double Bpm { get; set; }
    public double Duration { get; set; }
    public double? AvatarHeight { get; set; }
    public string Description { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string FolderName { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string FullFilePath { get; set; } = "";
    public string Hash { get; set; } = "";
    public bool HasOriginalMetadata { get; set; }
    public bool IsCameraScriptAuthorFromMetadata { get; set; }
    public bool IsMapIdFromMetadata { get; set; }
    public bool IsSongNameFromMetadata { get; set; }
    public bool IsSongSubNameFromMetadata { get; set; }
    public bool IsSongAuthorNameFromMetadata { get; set; }
    public bool IsLevelAuthorNameFromMetadata { get; set; }
    public bool IsBpmFromMetadata { get; set; }
    public bool IsAvatarHeightFromMetadata { get; set; }
    public bool IsDescriptionFromMetadata { get; set; }
    public int MovementCount { get; set; }
    public double ScriptDuration { get; set; }
    public double? SecondLargestDuration { get; set; }
    public double? SecondSmallestDuration { get; set; }
    public double? MedianDuration { get; set; }
    public double? ModeDuration { get; set; }
    public double OggDuration { get; set; }
}

public sealed class CachedOriginalScriptMatchResult
{
    public int MatchAlgorithmVersion { get; set; }
    public List<string> SearchPaths { get; set; } = new();
    public List<SearchCacheFileStamp> SourceFiles { get; set; } = new();
    public List<SearchCacheFileStamp> TargetFiles { get; set; } = new();
    public Dictionary<string, List<string>> MatchesByTargetFilePath { get; set; } = new();
}
