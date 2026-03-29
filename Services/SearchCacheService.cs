using System.IO;
using System.Text.Json;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public sealed class SearchCacheService
{
    private static readonly object SyncRoot = new();
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private static readonly string CacheFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "CameraScriptManager.SearchCache.json");

    private static SearchCacheDocument? _document;

    public bool TryGetSongScriptsSourceEntry(string sourcePath, out CachedSongScriptsSourceResult? entry)
    {
        lock (SyncRoot)
        {
            if (GetDocumentUnderLock().SongScriptsSources.TryGetValue(NormalizePathKey(sourcePath), out var cachedEntry))
            {
                entry = Clone(cachedEntry);
                return true;
            }

            entry = null;
            return false;
        }
    }

    public void SetSongScriptsSourceEntries(IEnumerable<CachedSongScriptsSourceResult> entries)
    {
        lock (SyncRoot)
        {
            var document = GetDocumentUnderLock();
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.SourceFile.Path))
                {
                    continue;
                }

                document.SongScriptsSources[NormalizePathKey(entry.SourceFile.Path)] = Clone(entry);
            }

            SaveDocumentUnderLock(document);
        }
    }

    public bool TryGetBeatmapFolderEntry(string folderPath, out CachedBeatmapFolderResult? entry)
    {
        lock (SyncRoot)
        {
            if (GetDocumentUnderLock().BeatmapFolders.TryGetValue(NormalizePathKey(folderPath), out var cachedEntry))
            {
                entry = Clone(cachedEntry);
                return true;
            }

            entry = null;
            return false;
        }
    }

    public void SetBeatmapFolderEntries(IEnumerable<CachedBeatmapFolderResult> entries)
    {
        lock (SyncRoot)
        {
            var document = GetDocumentUnderLock();
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    continue;
                }

                document.BeatmapFolders[NormalizePathKey(entry.FullPath)] = Clone(entry);
            }

            SaveDocumentUnderLock(document);
        }
    }

    public bool TryGetCameraScriptEntry(string fullFilePath, out CachedCameraScriptScanEntry? entry)
    {
        lock (SyncRoot)
        {
            if (GetDocumentUnderLock().CameraScriptEntries.TryGetValue(NormalizePathKey(fullFilePath), out var cachedEntry))
            {
                entry = Clone(cachedEntry);
                return true;
            }

            entry = null;
            return false;
        }
    }

    public void SetCameraScriptEntries(IEnumerable<CachedCameraScriptScanEntry> entries)
    {
        lock (SyncRoot)
        {
            var document = GetDocumentUnderLock();
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.SourceFile.Path))
                {
                    continue;
                }

                document.CameraScriptEntries[NormalizePathKey(entry.SourceFile.Path)] = Clone(entry);
            }

            SaveDocumentUnderLock(document);
        }
    }

    public CachedOriginalScriptMatchResult GetOriginalScriptMatch()
    {
        lock (SyncRoot)
        {
            return Clone(GetDocumentUnderLock().OriginalScriptMatch);
        }
    }

    public void SetOriginalScriptMatch(CachedOriginalScriptMatchResult result)
    {
        lock (SyncRoot)
        {
            var document = GetDocumentUnderLock();
            document.OriginalScriptMatch = Clone(result);
            SaveDocumentUnderLock(document);
        }
    }

    public static SearchCacheFileStamp? TryCreateFileStamp(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var fileInfo = new FileInfo(path);
            return new SearchCacheFileStamp
            {
                Path = NormalizeFileStampPath(path),
                Length = fileInfo.Length,
                CreationTimeUtc = fileInfo.CreationTimeUtc,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
            };
        }
        catch
        {
            return null;
        }
    }

    public static List<SearchCacheFileStamp> CollectFileStamps(IEnumerable<string> paths)
    {
        return paths
            .Select(TryCreateFileStamp)
            .Where(stamp => stamp != null)
            .Cast<SearchCacheFileStamp>()
            .OrderBy(stamp => stamp.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<SearchCacheFileStamp> CollectDirectoryFileStamps(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return new List<SearchCacheFileStamp>();
        }

        try
        {
            return CollectFileStamps(Directory.GetFiles(directoryPath, "*", SearchOption.TopDirectoryOnly));
        }
        catch
        {
            return new List<SearchCacheFileStamp>();
        }
    }

    public static bool IsSameFileStamp(SearchCacheFileStamp? left, SearchCacheFileStamp? right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }

        return string.Equals(NormalizeFileStampPath(left.Path), NormalizeFileStampPath(right.Path), StringComparison.OrdinalIgnoreCase) &&
            left.Length == right.Length &&
            left.CreationTimeUtc == right.CreationTimeUtc &&
            left.LastWriteTimeUtc == right.LastWriteTimeUtc;
    }

    public static bool AreSameFileStamps(IReadOnlyList<SearchCacheFileStamp>? left, IReadOnlyList<SearchCacheFileStamp>? right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!IsSameFileStamp(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static SearchCacheDocument GetDocumentUnderLock()
    {
        if (_document != null)
        {
            return _document;
        }

        try
        {
            if (File.Exists(CacheFilePath))
            {
                string json = File.ReadAllText(CacheFilePath);
                var loaded = JsonSerializer.Deserialize<SearchCacheDocument>(json, SerializerOptions);
                if (loaded != null && loaded.Version == SearchCacheDocument.CurrentVersion)
                {
                    EnsureSections(loaded);
                    _document = loaded;
                    return _document;
                }
            }
        }
        catch
        {
        }

        _document = new SearchCacheDocument();
        EnsureSections(_document);
        return _document;
    }

    private static void SaveDocumentUnderLock(SearchCacheDocument document)
    {
        try
        {
            document.Version = SearchCacheDocument.CurrentVersion;
            EnsureSections(document);

            string json = JsonSerializer.Serialize(document, SerializerOptions);
            string tempPath = CacheFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, CacheFilePath, true);
        }
        catch
        {
        }
    }

    private static void EnsureSections(SearchCacheDocument document)
    {
        document.SongScriptsSources ??= new Dictionary<string, CachedSongScriptsSourceResult>();
        document.BeatmapFolders ??= new Dictionary<string, CachedBeatmapFolderResult>();
        document.CameraScriptEntries ??= new Dictionary<string, CachedCameraScriptScanEntry>();
        document.OriginalScriptMatch ??= new CachedOriginalScriptMatchResult();
        document.OriginalScriptMatch.SearchPaths ??= new List<string>();
        document.OriginalScriptMatch.SourceFiles ??= new List<SearchCacheFileStamp>();
        document.OriginalScriptMatch.TargetFiles ??= new List<SearchCacheFileStamp>();
        document.OriginalScriptMatch.MatchesByTargetFilePath ??= new Dictionary<string, List<string>>();
    }

    private static string NormalizePathKey(string? path)
    {
        return NormalizeFileStampPath(path);
    }

    private static string NormalizeFileStampPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
        }

        return path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Trim()
            .ToLowerInvariant();
    }

    private static CachedSongScriptsSourceResult Clone(CachedSongScriptsSourceResult entry)
    {
        return new CachedSongScriptsSourceResult
        {
            SourceFile = Clone(entry.SourceFile),
            Entries = entry.Entries.Select(Clone).ToList()
        };
    }

    private static CachedSongScriptsEntry Clone(CachedSongScriptsEntry entry)
    {
        return new CachedSongScriptsEntry
        {
            SourceFilePath = entry.SourceFilePath,
            SourceRelativePath = entry.SourceRelativePath,
            ZipEntryName = entry.ZipEntryName,
            SourceDisplayPath = entry.SourceDisplayPath,
            FileName = entry.FileName,
            HasMetadataBlock = entry.HasMetadataBlock,
            MetadataMapId = entry.MetadataMapId,
            PathMapId = entry.PathMapId,
            MapId = entry.MapId,
            Hash = entry.Hash,
            CameraScriptAuthorName = entry.CameraScriptAuthorName,
            SongName = entry.SongName,
            SongSubName = entry.SongSubName,
            SongAuthorName = entry.SongAuthorName,
            LevelAuthorName = entry.LevelAuthorName,
            Bpm = entry.Bpm,
            Duration = entry.Duration,
            AvatarHeight = entry.AvatarHeight,
            Description = entry.Description
        };
    }

    private static CachedBeatmapFolderResult Clone(CachedBeatmapFolderResult entry)
    {
        return new CachedBeatmapFolderResult
        {
            FullPath = entry.FullPath,
            DisplayName = entry.DisplayName,
            FolderName = entry.FolderName,
            IsCustomLevels = entry.IsCustomLevels,
            IsCompatibleBeatmap = entry.IsCompatibleBeatmap,
            MapId = entry.MapId,
            Hash = entry.Hash,
            FileStamps = entry.FileStamps.Select(Clone).ToList()
        };
    }

    private static CachedCameraScriptScanEntry Clone(CachedCameraScriptScanEntry entry)
    {
        return new CachedCameraScriptScanEntry
        {
            SourceFile = Clone(entry.SourceFile),
            FolderFiles = entry.FolderFiles.Select(Clone).ToList(),
            Entry = Clone(entry.Entry)
        };
    }

    private static CachedCameraScriptEntry Clone(CachedCameraScriptEntry entry)
    {
        return new CachedCameraScriptEntry
        {
            MapId = entry.MapId,
            CameraScriptAuthorName = entry.CameraScriptAuthorName,
            SongName = entry.SongName,
            SongSubName = entry.SongSubName,
            SongAuthorName = entry.SongAuthorName,
            LevelAuthorName = entry.LevelAuthorName,
            Bpm = entry.Bpm,
            Duration = entry.Duration,
            AvatarHeight = entry.AvatarHeight,
            Description = entry.Description,
            FileName = entry.FileName,
            FolderPath = entry.FolderPath,
            FolderName = entry.FolderName,
            SourceType = entry.SourceType,
            FullFilePath = entry.FullFilePath,
            Hash = entry.Hash,
            HasOriginalMetadata = entry.HasOriginalMetadata,
            IsCameraScriptAuthorFromMetadata = entry.IsCameraScriptAuthorFromMetadata,
            IsMapIdFromMetadata = entry.IsMapIdFromMetadata,
            IsSongNameFromMetadata = entry.IsSongNameFromMetadata,
            IsSongSubNameFromMetadata = entry.IsSongSubNameFromMetadata,
            IsSongAuthorNameFromMetadata = entry.IsSongAuthorNameFromMetadata,
            IsLevelAuthorNameFromMetadata = entry.IsLevelAuthorNameFromMetadata,
            IsBpmFromMetadata = entry.IsBpmFromMetadata,
            IsAvatarHeightFromMetadata = entry.IsAvatarHeightFromMetadata,
            IsDescriptionFromMetadata = entry.IsDescriptionFromMetadata,
            MovementCount = entry.MovementCount,
            ScriptDuration = entry.ScriptDuration,
            OggDuration = entry.OggDuration
        };
    }

    private static CachedOriginalScriptMatchResult Clone(CachedOriginalScriptMatchResult entry)
    {
        return new CachedOriginalScriptMatchResult
        {
            SearchPaths = entry.SearchPaths.ToList(),
            SourceFiles = entry.SourceFiles.Select(Clone).ToList(),
            TargetFiles = entry.TargetFiles.Select(Clone).ToList(),
            MatchesByTargetFilePath = entry.MatchesByTargetFilePath.ToDictionary(
                pair => NormalizePathKey(pair.Key),
                pair => pair.Value.ToList(),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static SearchCacheFileStamp Clone(SearchCacheFileStamp entry)
    {
        return new SearchCacheFileStamp
        {
            Path = entry.Path,
            Length = entry.Length,
            CreationTimeUtc = entry.CreationTimeUtc,
            LastWriteTimeUtc = entry.LastWriteTimeUtc
        };
    }
}
