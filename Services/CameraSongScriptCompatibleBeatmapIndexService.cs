using System.IO;
using System.Threading;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public sealed class CameraSongScriptCompatibleBeatmapIndexService
{
    private readonly SongDetailsCacheService _cacheService;
    private readonly SearchCacheService _searchCacheService = new();

    public CameraSongScriptCompatibleBeatmapIndexService(SongDetailsCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public CameraSongScriptCompatibleBeatmapIndex ScanByMapId(
        string customLevelsPath,
        string customWipLevelsPath,
        Action<string, double?>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CameraSongScriptCompatibleBeatmapIndex();
        var candidates = new List<BeatmapFolderCandidate>();

        candidates.AddRange(GetFolderCandidates(customLevelsPath, isCustomLevels: true, includeCacheSubfolder: false));
        candidates.AddRange(GetFolderCandidates(customWipLevelsPath, isCustomLevels: false, includeCacheSubfolder: true));

        for (int index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BeatmapFolderCandidate candidate = candidates[index];
            double percent = candidates.Count == 0 ? 100 : ((index + 1) * 100.0 / candidates.Count);
            progress?.Invoke($"譜面ID参照読込中... {candidate.DisplayName}", percent);

            CompatibleBeatmapFolder beatmapFolder = CreateMapIdOnlyBeatmapFolder(candidate);
            result.BeatmapFolders.Add(beatmapFolder);
            AddLookup(result.ByMapId, beatmapFolder.MapId, beatmapFolder);

            if (!string.IsNullOrEmpty(beatmapFolder.MapId))
            {
                result.InstalledMapIds.Add(beatmapFolder.MapId);
            }
        }

        return result;
    }

    public CameraSongScriptCompatibleBeatmapIndex Scan(
        string customLevelsPath,
        string customWipLevelsPath,
        Action<string, double?>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CameraSongScriptCompatibleBeatmapIndex();
        var candidates = new List<BeatmapFolderCandidate>();
        var cacheUpdates = new List<CachedBeatmapFolderResult>();

        AddCustomLevelsInstalledMapIds(result.InstalledMapIds, customLevelsPath);

        candidates.AddRange(GetFolderCandidates(customLevelsPath, isCustomLevels: true, includeCacheSubfolder: false));
        candidates.AddRange(GetFolderCandidates(customWipLevelsPath, isCustomLevels: false, includeCacheSubfolder: true));

        try
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BeatmapFolderCandidate candidate = candidates[index];
                double percent = candidates.Count == 0 ? 100 : ((index + 1) * 100.0 / candidates.Count);
                progress?.Invoke($"hash検索中... {index + 1} / {candidates.Count}", percent);

                List<SearchCacheFileStamp> folderFileStamps = SearchCacheService.CollectDirectoryFileStamps(candidate.FullPath);
                CompatibleBeatmapFolder? beatmapFolder = null;
                bool isCompatibleBeatmap = false;

                if (_searchCacheService.TryGetBeatmapFolderEntry(candidate.FullPath, out var cachedEntry) &&
                    cachedEntry != null &&
                    SearchCacheService.AreSameFileStamps(cachedEntry.FileStamps, folderFileStamps))
                {
                    isCompatibleBeatmap = cachedEntry.IsCompatibleBeatmap;
                    if (isCompatibleBeatmap)
                    {
                        beatmapFolder = CreateBeatmapFolderFromCache(cachedEntry);
                    }
                }
                else if (TryCreateBeatmapFolder(candidate, out var createdBeatmapFolder))
                {
                    beatmapFolder = createdBeatmapFolder;
                    isCompatibleBeatmap = true;
                }

                cacheUpdates.Add(CreateBeatmapFolderCacheEntry(candidate, folderFileStamps, beatmapFolder, isCompatibleBeatmap));

                if (beatmapFolder == null)
                {
                    continue;
                }

                result.BeatmapFolders.Add(beatmapFolder);
                AddLookup(result.ByHash, beatmapFolder.Hash, beatmapFolder);
                AddLookup(result.ByMapId, beatmapFolder.MapId, beatmapFolder);

                if (!string.IsNullOrEmpty(beatmapFolder.MapId))
                {
                    result.InstalledMapIds.Add(beatmapFolder.MapId);
                }
            }
        }
        finally
        {
            _searchCacheService.SetBeatmapFolderEntries(cacheUpdates);
        }

        return result;
    }

    private static CompatibleBeatmapFolder CreateMapIdOnlyBeatmapFolder(BeatmapFolderCandidate candidate)
    {
        return new CompatibleBeatmapFolder
        {
            FolderName = Path.GetFileName(candidate.FullPath),
            DisplayName = candidate.DisplayName,
            FullPath = candidate.FullPath,
            IsCustomLevels = candidate.IsCustomLevels,
            MapId = ExtractLeadingMapIdCandidate(Path.GetFileName(candidate.FullPath))
        };
    }

    private static CompatibleBeatmapFolder CreateBeatmapFolderFromCache(CachedBeatmapFolderResult cacheEntry)
    {
        return new CompatibleBeatmapFolder
        {
            FolderName = cacheEntry.FolderName,
            DisplayName = cacheEntry.DisplayName,
            FullPath = cacheEntry.FullPath,
            IsCustomLevels = cacheEntry.IsCustomLevels,
            MapId = cacheEntry.MapId,
            Hash = cacheEntry.Hash
        };
    }

    private static CachedBeatmapFolderResult CreateBeatmapFolderCacheEntry(
        BeatmapFolderCandidate candidate,
        List<SearchCacheFileStamp> fileStamps,
        CompatibleBeatmapFolder? beatmapFolder,
        bool isCompatibleBeatmap)
    {
        return new CachedBeatmapFolderResult
        {
            FullPath = candidate.FullPath,
            DisplayName = candidate.DisplayName,
            FolderName = Path.GetFileName(candidate.FullPath),
            IsCustomLevels = candidate.IsCustomLevels,
            IsCompatibleBeatmap = isCompatibleBeatmap,
            MapId = beatmapFolder?.MapId ?? string.Empty,
            Hash = beatmapFolder?.Hash ?? string.Empty,
            FileStamps = fileStamps.Select(CloneFileStamp).ToList()
        };
    }

    private static void AddCustomLevelsInstalledMapIds(ISet<string> installedMapIds, string customLevelsPath)
    {
        if (string.IsNullOrWhiteSpace(customLevelsPath) || !Directory.Exists(customLevelsPath))
        {
            return;
        }

        try
        {
            foreach (string folderPath in Directory.GetDirectories(customLevelsPath, "*", SearchOption.TopDirectoryOnly))
            {
                string mapId = ExtractLeadingMapIdCandidate(Path.GetFileName(folderPath));
                if (!string.IsNullOrEmpty(mapId))
                {
                    installedMapIds.Add(mapId);
                }
            }
        }
        catch
        {
        }
    }

    private static IEnumerable<BeatmapFolderCandidate> GetFolderCandidates(
        string rootPath,
        bool isCustomLevels,
        bool includeCacheSubfolder)
    {
        var results = new List<BeatmapFolderCandidate>();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return results;
        }

        try
        {
            foreach (string folderPath in Directory.GetDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
            {
                string folderName = Path.GetFileName(folderPath);
                if (!isCustomLevels && includeCacheSubfolder && string.Equals(folderName, "Cache", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(new BeatmapFolderCandidate
                {
                    FullPath = folderPath,
                    DisplayName = folderName,
                    IsCustomLevels = isCustomLevels
                });
            }

            if (!isCustomLevels && includeCacheSubfolder)
            {
                string cacheRootPath = Path.Combine(rootPath, "Cache");
                if (Directory.Exists(cacheRootPath))
                {
                    foreach (string folderPath in Directory.GetDirectories(cacheRootPath, "*", SearchOption.TopDirectoryOnly))
                    {
                        string folderName = Path.GetFileName(folderPath);
                        results.Add(new BeatmapFolderCandidate
                        {
                            FullPath = folderPath,
                            DisplayName = Path.Combine("Cache", folderName),
                            IsCustomLevels = false
                        });
                    }
                }
            }
        }
        catch
        {
        }

        return results
            .OrderBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryCreateBeatmapFolder(BeatmapFolderCandidate candidate, out CompatibleBeatmapFolder beatmapFolder)
    {
        beatmapFolder = new CompatibleBeatmapFolder();

        InfoDatData? infoDat = InfoDatReader.ReadFromFolder(candidate.FullPath);
        if (infoDat == null)
        {
            return false;
        }

        string songFileName = infoDat.SongFileName;
        if (string.IsNullOrWhiteSpace(songFileName))
        {
            return false;
        }

        string songFilePath = Path.Combine(candidate.FullPath, songFileName);
        if (!File.Exists(songFilePath))
        {
            return false;
        }

        string hash = NormalizeHash(HashCalculator.CalculateSongHash(candidate.FullPath, infoDat));
        if (string.IsNullOrEmpty(hash))
        {
            return false;
        }

        string mapId = ResolveBeatmapMapId(hash, candidate.FullPath);

        beatmapFolder = new CompatibleBeatmapFolder
        {
            FolderName = Path.GetFileName(candidate.FullPath),
            DisplayName = candidate.DisplayName,
            FullPath = candidate.FullPath,
            IsCustomLevels = candidate.IsCustomLevels,
            MapId = mapId,
            Hash = hash
        };
        return true;
    }

    private string ResolveBeatmapMapId(string hash, string folderPath)
    {
        if (!string.IsNullOrEmpty(hash) &&
            _cacheService.TryGetByHash(hash, out var response) &&
            !string.IsNullOrWhiteSpace(response.Id))
        {
            return NormalizeMapId(response.Id);
        }

        return ExtractLeadingMapIdCandidate(Path.GetFileName(folderPath));
    }

    private static void AddLookup(
        IDictionary<string, List<CompatibleBeatmapFolder>> lookup,
        string key,
        CompatibleBeatmapFolder beatmapFolder)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (!lookup.TryGetValue(key, out var list))
        {
            list = new List<CompatibleBeatmapFolder>();
            lookup[key] = list;
        }

        list.Add(beatmapFolder);
    }

    private static string NormalizeMapId(string? mapId)
    {
        return string.IsNullOrWhiteSpace(mapId)
            ? string.Empty
            : mapId.Trim().ToLowerInvariant();
    }

    private static string NormalizeHash(string? hash)
    {
        return string.IsNullOrWhiteSpace(hash)
            ? string.Empty
            : hash.Trim().ToLowerInvariant();
    }

    public static string ExtractLeadingMapIdCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        int length = 0;
        while (length < value.Length && IsHexCharacter(value[length]))
        {
            length++;
        }

        if (length == 0 || length > 6)
        {
            return string.Empty;
        }

        return value[..length].ToLowerInvariant();
    }

    private static bool IsHexCharacter(char value)
    {
        return (value >= '0' && value <= '9') ||
            (value >= 'a' && value <= 'f') ||
            (value >= 'A' && value <= 'F');
    }

    private static SearchCacheFileStamp CloneFileStamp(SearchCacheFileStamp stamp)
    {
        return new SearchCacheFileStamp
        {
            Path = stamp.Path,
            Length = stamp.Length,
            CreationTimeUtc = stamp.CreationTimeUtc,
            LastWriteTimeUtc = stamp.LastWriteTimeUtc
        };
    }

    private sealed class BeatmapFolderCandidate
    {
        public string FullPath { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsCustomLevels { get; set; }
    }

    public sealed class CompatibleBeatmapFolder
    {
        public string FolderName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsCustomLevels { get; set; }
        public string MapId { get; set; } = "";
        public string Hash { get; set; } = "";

        public SongScriptsMatchedBeatmapFolder ToMatchedFolder()
        {
            return new SongScriptsMatchedBeatmapFolder
            {
                FolderName = FolderName,
                DisplayName = DisplayName,
                FullPath = FullPath,
                IsCustomLevels = IsCustomLevels
            };
        }
    }
}

public sealed class CameraSongScriptCompatibleBeatmapIndex
{
    public List<CameraSongScriptCompatibleBeatmapIndexService.CompatibleBeatmapFolder> BeatmapFolders { get; } = new();
    public Dictionary<string, List<CameraSongScriptCompatibleBeatmapIndexService.CompatibleBeatmapFolder>> ByMapId { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<CameraSongScriptCompatibleBeatmapIndexService.CompatibleBeatmapFolder>> ByHash { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> InstalledMapIds { get; } = new(StringComparer.OrdinalIgnoreCase);
}
