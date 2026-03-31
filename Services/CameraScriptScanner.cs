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
        entry.SecondLargestDuration = scriptMetrics.SecondLargestDuration;
        entry.SecondSmallestDuration = scriptMetrics.SecondSmallestDuration;
        entry.MedianDuration = scriptMetrics.MedianDuration;
        entry.ModeDuration = scriptMetrics.ModeDuration;

        if (CameraScriptMetadataReader.TryRead(jsonContent, out var metadata))
        {
            entry.HasOriginalMetadata = true;

            if (metadata.HasMapId)
            {
                entry.MapId = metadata.MapId;
                entry.IsMapIdFromMetadata = !string.IsNullOrWhiteSpace(entry.MapId);
            }

            if (metadata.HasHash)
            {
                entry.Hash = metadata.Hash;
            }

            if (metadata.HasCameraScriptAuthorName)
            {
                entry.CameraScriptAuthorName = metadata.CameraScriptAuthorName;
                entry.IsCameraScriptAuthorFromMetadata = !string.IsNullOrWhiteSpace(entry.CameraScriptAuthorName);
            }

            if (metadata.HasBpm)
            {
                entry.Bpm = metadata.Bpm;
                entry.IsBpmFromMetadata = entry.Bpm > 0;
            }

            if (metadata.HasDuration)
            {
                entry.Duration = metadata.Duration;
            }

            if (metadata.HasSongName)
            {
                entry.SongName = metadata.SongName;
                entry.IsSongNameFromMetadata = !string.IsNullOrWhiteSpace(entry.SongName);
            }

            if (metadata.HasSongSubName)
            {
                entry.SongSubName = metadata.SongSubName;
                entry.IsSongSubNameFromMetadata = true;
            }

            if (metadata.HasSongAuthorName)
            {
                entry.SongAuthorName = metadata.SongAuthorName;
                entry.IsSongAuthorNameFromMetadata = !string.IsNullOrWhiteSpace(entry.SongAuthorName);
            }

            if (metadata.HasLevelAuthorName)
            {
                entry.LevelAuthorName = metadata.LevelAuthorName;
                entry.IsLevelAuthorNameFromMetadata = !string.IsNullOrWhiteSpace(entry.LevelAuthorName);
            }

            if (metadata.HasAvatarHeight)
            {
                entry.AvatarHeight = metadata.AvatarHeight;
                entry.IsAvatarHeightFromMetadata = true;
            }

            if (metadata.HasDescription)
            {
                entry.Description = metadata.Description;
                entry.IsDescriptionFromMetadata = true;
            }
        }

        // metadataが無い場合のみ、フォルダ名からmapIdを補完する
        if (!entry.HasOriginalMetadata && string.IsNullOrWhiteSpace(entry.MapId))
        {
            entry.MapId = HexIdExtractor.ExtractHexId(folderName) ?? "";
        }

        // Read Info.dat only for song hash calculation.
        var infoDat = InfoDatReader.ReadFromFolder(folderPath);
        if (infoDat != null)
        {
            // Calculate Song Hash based on Info.dat and associated beatmap files
            if (infoDat.BeatmapFilenames.Count > 0)
            {
                string calculatedHash = HashCalculator.CalculateSongHash(folderPath, infoDat);
                if (!entry.HasOriginalMetadata || string.IsNullOrWhiteSpace(entry.Hash))
                {
                    entry.Hash = calculatedHash;
                }
            }
        }

        return entry;
    }

    /// <summary>
    /// SongScript JSONのMovements配列からDurationとDelayの合計値（秒）を計算する。
    /// </summary>
    private static (int MovementCount, double TotalDurationAndDelay, double? SecondLargestDuration, double? SecondSmallestDuration, double? MedianDuration, double? ModeDuration) GetScriptMetrics(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            if (!doc.RootElement.TryGetProperty("Movements", out var movements))
                return (0, 0, null, null, null, null);

            int movementCount = 0;
            double total = 0;
            var durations = new List<double>();
            foreach (var movement in movements.EnumerateArray())
            {
                movementCount++;
                double durationValue = 0;
                if (movement.TryGetProperty("Duration", out var duration))
                    durationValue = duration.GetDouble();
                durations.Add(durationValue);
                total += durationValue;
                if (movement.TryGetProperty("Delay", out var delay))
                    total += delay.GetDouble();
            }

            durations.Sort();
            return (
                movementCount,
                total,
                GetSecondLargestDuration(durations),
                GetSecondSmallestDuration(durations),
                GetMedianDuration(durations),
                GetModeDuration(durations));
        }
        catch
        {
            return (0, 0, null, null, null, null);
        }
    }

    private static double? GetSecondLargestDuration(IReadOnlyList<double> sortedDurations)
    {
        return sortedDurations.Count >= 2 ? sortedDurations[^2] : null;
    }

    private static double? GetSecondSmallestDuration(IReadOnlyList<double> sortedDurations)
    {
        return sortedDurations.Count >= 2 ? sortedDurations[1] : null;
    }

    private static double? GetMedianDuration(IReadOnlyList<double> sortedDurations)
    {
        if (sortedDurations.Count == 0)
        {
            return null;
        }

        int middleIndex = sortedDurations.Count / 2;
        if (sortedDurations.Count % 2 == 1)
        {
            return sortedDurations[middleIndex];
        }

        return (sortedDurations[middleIndex - 1] + sortedDurations[middleIndex]) / 2.0;
    }

    private static double? GetModeDuration(IReadOnlyList<double> durations)
    {
        if (durations.Count == 0)
        {
            return null;
        }

        const double modeBucketScale = 10.0;
        var grouped = durations
            .Select(duration => (int)Math.Round(duration * modeBucketScale, MidpointRounding.AwayFromZero))
            .GroupBy(bucket => bucket)
            .Select(group => new { Bucket = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Bucket)
            .ToList();

        if (grouped.Count == 0)
        {
            return null;
        }

        int topCount = grouped[0].Count;
        if (grouped.Count(group => group.Count == topCount) > 1)
        {
            return null;
        }

        return grouped[0].Bucket / modeBucketScale;
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
            SecondLargestDuration = entry.SecondLargestDuration,
            SecondSmallestDuration = entry.SecondSmallestDuration,
            MedianDuration = entry.MedianDuration,
            ModeDuration = entry.ModeDuration,
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
            SecondLargestDuration = entry.SecondLargestDuration,
            SecondSmallestDuration = entry.SecondSmallestDuration,
            MedianDuration = entry.MedianDuration,
            ModeDuration = entry.ModeDuration,
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
