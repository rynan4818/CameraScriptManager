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
        try
        {
            foreach (var dir in Directory.GetDirectories(rootPath))
            {
                var folderName = Path.GetFileName(dir);
                var jsonFiles = Directory.GetFiles(dir, "*.json");

                foreach (var jsonFile in jsonFiles)
                {
                    var fileName = Path.GetFileName(jsonFile);
                    if (SkipFileNames.Contains(fileName))
                        continue;

                    try
                    {
                        var content = File.ReadAllText(jsonFile);
                        if (!IsValidCameraScript(content))
                            continue;

                        var entry = CreateEntry(content, jsonFile, dir, folderName, sourceType);
                        results.Add(entry);
                    }
                    catch { }
                }
            }
        }
        catch { }
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
            ScriptDuration = CalculateScriptDuration(jsonContent),
            OggDuration = _oggDurationService.GetDurationFromFolder(folderPath)
        };

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
                    entry.AvatarHeight = avatarHeight.GetDouble();
                    entry.IsAvatarHeightFromMetadata = entry.AvatarHeight > 0;
                }

                if (metadata.TryGetProperty("description", out var descriptionProp))
                {
                    entry.Description = descriptionProp.GetString() ?? "";
                    entry.IsDescriptionFromMetadata = true;
                }
            }

        }
        catch { }

        // Extract mapId from folder name if not found in metadata
        if (string.IsNullOrWhiteSpace(entry.MapId))
        {
            entry.MapId = HexIdExtractor.ExtractHexId(folderName) ?? "";
        }

        // Read Info.dat for supplementary data
        var infoDat = InfoDatReader.ReadFromFolder(folderPath);
        if (infoDat != null)
        {
            bool supplemented = false;

            if (string.IsNullOrWhiteSpace(entry.SongName) && !string.IsNullOrWhiteSpace(infoDat.SongName))
            {
                entry.SongName = infoDat.SongName;
                supplemented = true;
            }

            if (string.IsNullOrWhiteSpace(entry.SongSubName) && !string.IsNullOrWhiteSpace(infoDat.SongSubName))
            {
                entry.SongSubName = infoDat.SongSubName;
                supplemented = true;
            }

            if (string.IsNullOrWhiteSpace(entry.SongAuthorName) && !string.IsNullOrWhiteSpace(infoDat.SongAuthorName))
            {
                entry.SongAuthorName = infoDat.SongAuthorName;
                supplemented = true;
            }

            if (string.IsNullOrWhiteSpace(entry.LevelAuthorName) && !string.IsNullOrWhiteSpace(infoDat.LevelAuthorName))
            {
                entry.LevelAuthorName = infoDat.LevelAuthorName;
                supplemented = true;
            }

            if (entry.Bpm == 0 && infoDat.Bpm > 0)
            {
                entry.Bpm = infoDat.Bpm;
                supplemented = true;
            }

            // Calculate Song Hash based on Info.dat and associated beatmap files
            if (infoDat.BeatmapFilenames.Count > 0)
            {
                entry.Hash = HashCalculator.CalculateSongHash(folderPath, infoDat.BeatmapFilenames);
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
    private static double CalculateScriptDuration(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            if (!doc.RootElement.TryGetProperty("Movements", out var movements))
                return 0;

            double total = 0;
            foreach (var movement in movements.EnumerateArray())
            {
                if (movement.TryGetProperty("Duration", out var duration))
                    total += duration.GetDouble();
                if (movement.TryGetProperty("Delay", out var delay))
                    total += delay.GetDouble();
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }
}
