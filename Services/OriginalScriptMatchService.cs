using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public class OriginalScriptMatchService
{
    private readonly string[] _searchPaths;
    private readonly Action<string>? _progressCallback;

    public OriginalScriptMatchService(IEnumerable<string> searchPaths, Action<string>? progressCallback = null)
    {
        _searchPaths = searchPaths.Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p)).ToArray();
        _progressCallback = progressCallback;
    }

    public async Task MatchOriginalScriptsAsync(IReadOnlyList<CameraScriptEntry> targetEntries)
    {
        if (_searchPaths.Length == 0 || targetEntries.Count == 0)
            return;

        // Collect all possible source files (.json and .zip)
        var sourceFiles = new List<string>();
        foreach (var path in _searchPaths)
        {
            _progressCallback?.Invoke($"検索パスをスキャン中: {path}");
            await Task.Run(() =>
            {
                CollectFiles(path, sourceFiles);
            });
        }

        int totalFiles = sourceFiles.Count;
        int currentFile = 0;

        foreach (var sourceFile in sourceFiles)
        {
            currentFile++;
            if (currentFile % 10 == 0)
                _progressCallback?.Invoke($"元データ照合中... ({currentFile}/{totalFiles})");

            bool isZip = sourceFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                try
                {
                    if (isZip)
                    {
                        ProcessZipFile(sourceFile, targetEntries);
                    }
                    else
                    {
                        ProcessJsonFile(sourceFile, targetEntries);
                    }
                }
                catch
                {
                    // Ignore errors for individual files
                }
            });
        }
    }

    private void CollectFiles(string path, List<string> sourceFiles)
    {
        try
        {
            sourceFiles.AddRange(Directory.GetFiles(path, "*.json", SearchOption.AllDirectories));
            sourceFiles.AddRange(Directory.GetFiles(path, "*.zip", SearchOption.AllDirectories));
        }
        catch
        {
            // Ignore access errors
        }
    }

    private void ProcessJsonFile(string jsonFilePath, IReadOnlyList<CameraScriptEntry> targetEntries)
    {
        var content = File.ReadAllText(jsonFilePath);
        var sourceInfo = ParseCameraScriptInfo(content);
        if (sourceInfo == null) return;

        string fileName = Path.GetFileName(jsonFilePath);
        string folderName = Path.GetFileName(Path.GetDirectoryName(jsonFilePath) ?? "");

        CheckAndApplyMatch(targetEntries, sourceInfo, fileName, folderName, fileName);
    }

    private void ProcessZipFile(string zipFilePath, IReadOnlyList<CameraScriptEntry> targetEntries)
    {
        using var archive = ZipFile.OpenRead(zipFilePath);
        string zipFileName = Path.GetFileName(zipFilePath);

        foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            // Skip typical junk files
            if (entry.Name.Equals("info.dat", StringComparison.OrdinalIgnoreCase) ||
                entry.Name.Equals("cinema-video.json", StringComparison.OrdinalIgnoreCase))
                continue;

            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            var sourceInfo = ParseCameraScriptInfo(content);
            if (sourceInfo == null) continue;

            string folderName = Path.GetDirectoryName(entry.FullName)?.Replace('\\', '/') ?? "";
            string displayName = string.IsNullOrEmpty(folderName) ? $"{zipFileName}/{entry.Name}" : $"{zipFileName}/{folderName}/{entry.Name}";

            CheckAndApplyMatch(targetEntries, sourceInfo, entry.Name, string.IsNullOrEmpty(folderName) ? zipFileName : folderName, displayName);
        }
    }

    private void CheckAndApplyMatch(
        IReadOnlyList<CameraScriptEntry> targetEntries, 
        ScriptMatchInfo sourceInfo, 
        string sourceFileName, 
        string sourceFolderName, 
        string displayName)
    {
        foreach (var target in targetEntries)
        {
            if (!string.IsNullOrEmpty(target.OriginalSourceFile))
                continue; // Already matched

            // 1. Check MapId if available in folder name or file name
            string? sourceIdFromFolder = HexIdExtractor.ExtractHexId(sourceFolderName);
            string? sourceIdFromFile = HexIdExtractor.ExtractHexId(sourceFileName);
            
            bool idMatches = true;
            if (!string.IsNullOrWhiteSpace(target.MapId))
            {
                if (!string.IsNullOrWhiteSpace(sourceIdFromFolder) && target.MapId != sourceIdFromFolder) idMatches = false;
                else if (!string.IsNullOrWhiteSpace(sourceIdFromFile) && target.MapId != sourceIdFromFile) idMatches = false;
            }

            if (!idMatches) continue;

            // 2. Parse target script info if needed
            var targetInfo = ParseCameraScriptInfo(target.JsonContent);
            if (targetInfo == null) continue;

            // 3. Match conditions
            if (sourceInfo.MovementCount == targetInfo.MovementCount &&
                Math.Abs(sourceInfo.TotalDurationAndDelay - targetInfo.TotalDurationAndDelay) <= 0.1)
            {
                target.OriginalSourceFile = displayName;
            }
        }
    }

    private ScriptMatchInfo? ParseCameraScriptInfo(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("Movements", out var movements) || movements.ValueKind != JsonValueKind.Array)
                return null;

            int count = 0;
            double totalTime = 0;

            foreach (var movement in movements.EnumerateArray())
            {
                count++;
                if (movement.TryGetProperty("Duration", out var durationInfo))
                {
                    totalTime += durationInfo.GetDouble();
                }
                if (movement.TryGetProperty("Delay", out var delayInfo))
                {
                    totalTime += delayInfo.GetDouble();
                }
            }

            return new ScriptMatchInfo
            {
                MovementCount = count,
                TotalDurationAndDelay = totalTime
            };
        }
        catch
        {
            return null;
        }
    }

    private class ScriptMatchInfo
    {
        public int MovementCount { get; set; }
        public double TotalDurationAndDelay { get; set; }
    }
}
