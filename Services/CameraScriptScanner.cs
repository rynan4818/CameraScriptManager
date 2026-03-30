using System.IO;
using System.Text.Json;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public class CameraScriptScanner
{
    private static readonly HashSet<string> SkipFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "info.dat",
        "BPMInfo.dat",
        "cinema-video.json",
        "AudioData.dat"
    };

    private readonly OggDurationService _oggDurationService = new();
    private readonly SearchCacheService _searchCacheService = new();

    public List<CameraScriptEntry> Scan(string customLevelsPath, string customWIPLevelsPath)
    {
        var results = new List<CameraScriptEntry>();

        if (!string.IsNullOrWhiteSpace(customLevelsPath) && Directory.Exists(customLevelsPath))
            ScanFolder(customLevelsPath, "CustomLevels", results);

        if (!string.IsNullOrWhiteSpace(customWIPLevelsPath) && Directory.Exists(customWIPLevelsPath))
            ScanFolder(customWIPLevelsPath, "CustomWIPLevels", results);

        return results;
    }

    private void ScanFolder(string rootPath, string sourceType, List<CameraScriptEntry> results)
    {
        var cacheUpdates = new List<CachedCameraScriptScanEntry>();

        try
        {
            foreach (var dir in Directory.GetDirectories(rootPath))
            {
                var folderName = Path.GetFileName(dir);
                var jsonFiles = Directory.GetFiles(dir, "*.json");
                var folderFileStamps = SearchCacheService.CollectDirectoryFileStamps(dir);

                foreach (var jsonFile in jsonFiles)
                {
                    var fileName = Path.GetFileName(jsonFile);
                    if (SkipFileNames.Contains(fileName))
                        continue;

                    try
                    {
                        SearchCacheFileStamp? sourceFileStamp = SearchCacheService.TryCreateFileStamp(jsonFile);
                        CameraScriptEntry? entry = null;

                        if (sourceFileStamp != null &&
                            _searchCacheService.TryGetCameraScriptEntry(jsonFile, out var cachedEntry) &&
                            cachedEntry != null &&
                            SearchCacheService.IsSameFileStamp(cachedEntry.SourceFile, sourceFileStamp) &&
                            SearchCacheService.AreSameFileStamps(cachedEntry.FolderFiles, folderFileStamps))
                        {
                            entry = CreateRuntimeEntryFromCache(cachedEntry.Entry);
                        }
                        else
                        {
                            var content = File.ReadAllText(jsonFile);
                            if (!IsValidCameraScript(content))
                                continue;

                            entry = CreateEntry(content, jsonFile, dir, folderName, sourceType);
                        }

                        if (entry == null)
                        {
                            continue;
                        }

                        results.Add(entry);

                        if (sourceFileStamp != null)
                        {
                            cacheUpdates.Add(new CachedCameraScriptScanEntry
                            {
                                SourceFile = sourceFileStamp,
                                FolderFiles = folderFileStamps.Select(CloneFileStamp).ToList(),
                                Entry = CreateCacheEntry(entry)
                            });
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        _searchCacheService.SetCameraScriptEntries(cacheUpdates);
    }

    private static bool IsValidCameraScript(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return root.TryGetProperty("Movements", out var movements)
                && movements.ValueKind == JsonValueKind.Array;
        }
        catch
        {
            return false;
        }
    }

    private CameraScriptEntry CreateEntry(string jsonContent, string fullPath, string folderPath, string folderName, string sourceType)
    {
        var entry = new CameraScriptEntry
        {
            FileName = Path.GetFileName(fullPath),
            FolderPath = folderPath,
            FolderName = folderName,
            SourceType = sourceType,
            FullFilePath = fullPath,
            JsonContent = jsonContent,
            OggDuration = _oggDurationService.GetDurationFromFolder(folderPath)
        };

        var scriptMetrics = GetScriptMetrics(jsonContent);
        entry.MovementCount = scriptMetrics.MovementCount;
        entry.ScriptDuration = scriptMetrics.TotalDurationAndDelay;

        // Try read metadata from JSON
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("metadata", out var metadata))
            {
                entry.HasOriginalMetadata = true;

                if (metadata.TryGetProperty("mapId", out var mapId))
                {
                    entry.MapId = mapId.GetString() ?? "";
                    entry.IsMapIdFromMetadata = !string.IsNullOrWhiteSpace(entry.MapId);
                }

                if (metadata.TryGetProperty("cameraScriptAuthorName", out var author))
                {
                    entry.CameraScriptAuthorName = author.GetString() ?? "";
                    entry.IsCameraScriptAuthorFromMetadata = !string.IsNullOrWhiteSpace(entry.CameraScriptAuthorName);
                }

                if (metadata.TryGetProperty("bpm", out var bpm))
                {
                    entry.Bpm = bpm.GetDouble();
                    entry.IsBpmFromMetadata = entry.Bpm > 0;
                }

                if (metadata.TryGetProperty("duration", out var duration))
                    entry.Duration = duration.GetDouble();

                if (metadata.TryGetProperty("songName", out var songName))
                {
                    entry.SongName = songName.GetString() ?? "";
                    entry.IsSongNameFromMetadata = !string.IsNullOrWhiteSpace(entry.SongName);
                }

                if (metadata.TryGetProperty("songSubName", out var songSubName))
                {
                    entry.SongSubName = songSubName.GetString() ?? "";
                    entry.IsSongSubNameFromMetadata = true; // SubName can be empty sometimes, but if it exists, it's locked. Check if property is there
                }

                if (metadata.TryGetProperty("songAuthorName", out var songAuthor))
                {
                    entry.SongAuthorName = songAuthor.GetString() ?? "";
                    entry.IsSongAuthorNameFromMetadata = !string.IsNullOrWhiteSpace(entry.SongAuthorName);
                }

                if (metadata.TryGetProperty("levelAuthorName", out var levelAuthor))
                {
                    entry.LevelAuthorName = levelAuthor.GetString() ?? "";
                    entry.IsLevelAuthorNameFromMetadata = !string.IsNullOrWhiteSpace(entry.LevelAuthorName);
                }

                if (metadata.TryGetProperty("avatarHeight", out var avatarHeight))
                {
                    if (TryReadDouble(avatarHeight, out double avatarHeightValue))
                    {
                        entry.AvatarHeight = avatarHeightValue;
                    }

                    entry.IsAvatarHeightFromMetadata = true;
                }

                if (metadata.TryGetProperty("description", out var descriptionProp))
                {
                    entry.Description = descriptionProp.GetString() ?? "";
                    entry.IsDescriptionFromMetadata = true;
                }
            }

        }
        catch { }

        // metadataが無い場合のみ、フォルダ名からmapIdを補完する
        if (!entry.HasOriginalMetadata && string.IsNullOrWhiteSpace(entry.MapId))
        {
            entry.MapId = HexIdExtractor.ExtractHexId(folderName) ?? "";
        }

        // Read Info.dat for supplementary data
        var infoDat = InfoDatReader.ReadFromFolder(folderPath);
        if (infoDat != null)
        {
            bool supplemented = false;

            if (!entry.HasOriginalMetadata &&
                string.IsNullOrWhiteSpace(entry.SongName) &&
                !string.IsNullOrWhiteSpace(infoDat.SongName))
            {
                entry.SongName = infoDat.SongName;
                supplemented = true;
            }

            if (!entry.HasOriginalMetadata &&
                string.IsNullOrWhiteSpace(entry.SongSubName) &&
                !string.IsNullOrWhiteSpace(infoDat.SongSubName))
            {
                entry.SongSubName = infoDat.SongSubName;
                supplemented = true;
            }

            if (!entry.HasOriginalMetadata &&
                string.IsNullOrWhiteSpace(entry.SongAuthorName) &&
                !string.IsNullOrWhiteSpace(infoDat.SongAuthorName))
            {
                entry.SongAuthorName = infoDat.SongAuthorName;
                supplemented = true;
            }

            if (!entry.HasOriginalMetadata &&
                string.IsNullOrWhiteSpace(entry.LevelAuthorName) &&
                !string.IsNullOrWhiteSpace(infoDat.LevelAuthorName))
            {
                entry.LevelAuthorName = infoDat.LevelAuthorName;
                supplemented = true;
            }

            if (!entry.HasOriginalMetadata && entry.Bpm == 0 && infoDat.Bpm > 0)
            {
                entry.Bpm = infoDat.Bpm;
                supplemented = true;
            }

            // Calculate Song Hash based on Info.dat and associated beatmap files
            if (infoDat.BeatmapFilenames.Count > 0)
            {
                entry.Hash = HashCalculator.CalculateSongHash(folderPath, infoDat);
            }

            // If no original metadata but Info.dat provided data, mark as needing metadata write
            if (!entry.HasOriginalMetadata && supplemented)
            {
                // Will be used to set IsModified = true in the ViewModel
                entry.HasOriginalMetadata = false;
            }
        }

        return entry;
    }

    /// <summary>
    /// SongScript JSONのMovements配列からDurationとDelayの合計値（秒）を計算する。
    /// </summary>
    private static (int MovementCount, double TotalDurationAndDelay) GetScriptMetrics(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            if (!doc.RootElement.TryGetProperty("Movements", out var movements))
                return (0, 0);

            int movementCount = 0;
            double total = 0;
            foreach (var movement in movements.EnumerateArray())
            {
                movementCount++;
                if (movement.TryGetProperty("Duration", out var duration))
                    total += duration.GetDouble();
                if (movement.TryGetProperty("Delay", out var delay))
                    total += delay.GetDouble();
            }
            return (movementCount, total);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static bool TryReadDouble(JsonElement property, out double value)
    {
        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out value))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String &&
            double.TryParse(property.GetString(), out double parsed))
        {
            value = parsed;
            return true;
        }

        value = 0;
        return false;
    }

    private static CameraScriptEntry CreateRuntimeEntryFromCache(CachedCameraScriptEntry entry)
    {
        return new CameraScriptEntry
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
            OggDuration = entry.OggDuration,
            OriginalSourceFiles = new List<string>()
        };
    }

    private static CachedCameraScriptEntry CreateCacheEntry(CameraScriptEntry entry)
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
}
