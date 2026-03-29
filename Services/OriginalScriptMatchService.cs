using System.IO;
using System.Text;
using System.Text.Json;
using CameraScriptManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace CameraScriptManager.Services;

public class OriginalScriptMatchService
{
    private readonly string[] _searchPaths;
    private readonly Action<string, double?>? _progressCallback;
    private readonly SearchCacheService _searchCacheService = new();

    public OriginalScriptMatchService(IEnumerable<string> searchPaths, Action<string, double?>? progressCallback = null)
    {
        _searchPaths = searchPaths.Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p)).ToArray();
        _progressCallback = progressCallback;
    }

    public async Task MatchOriginalScriptsAsync(IReadOnlyList<CameraScriptEntry> targetEntries)
    {
        foreach (var targetEntry in targetEntries)
        {
            targetEntry.OriginalSourceFiles.Clear();
        }

        if (_searchPaths.Length == 0 || targetEntries.Count == 0)
            return;

        // Collect all possible source files (.json and .zip)
        var sourceFiles = new List<string>();
        foreach (var path in _searchPaths)
        {
            _progressCallback?.Invoke($"検索パスをスキャン中: {path}", null);
            await Task.Run(() =>
            {
                CollectFiles(path, sourceFiles);
            });
        }

        sourceFiles = sourceFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<SearchCacheFileStamp> sourceFileStamps = SearchCacheService.CollectFileStamps(sourceFiles);
        List<SearchCacheFileStamp> targetFileStamps = SearchCacheService.CollectFileStamps(
            targetEntries
                .Select(entry => entry.FullFilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path)));

        var cachedResult = _searchCacheService.GetOriginalScriptMatch();
        if (AreSameSearchPaths(cachedResult.SearchPaths, _searchPaths) &&
            SearchCacheService.AreSameFileStamps(cachedResult.SourceFiles, sourceFileStamps) &&
            SearchCacheService.AreSameFileStamps(cachedResult.TargetFiles, targetFileStamps))
        {
            _progressCallback?.Invoke("元データ照合キャッシュを適用中...", 100);
            ApplyCachedMatches(targetEntries, cachedResult);
            return;
        }

        int totalFiles = sourceFiles.Count;
        int currentFile = 0;

        foreach (var sourceFile in sourceFiles)
        {
            currentFile++;
            if (currentFile % 10 == 0 || currentFile == totalFiles)
                _progressCallback?.Invoke($"元データ照合中... ({currentFile}/{totalFiles})", (double)currentFile / totalFiles * 100);

            bool isArchive = !sourceFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                try
                {
                    if (isArchive)
                    {
                        ProcessArchiveFile(sourceFile, targetEntries);
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

        _searchCacheService.SetOriginalScriptMatch(new CachedOriginalScriptMatchResult
        {
            SearchPaths = _searchPaths.Select(NormalizePath).ToList(),
            SourceFiles = sourceFileStamps,
            TargetFiles = targetFileStamps,
            MatchesByTargetFilePath = targetEntries.ToDictionary(
                target => NormalizePath(target.FullFilePath),
                target => target.OriginalSourceFiles.ToList(),
                StringComparer.OrdinalIgnoreCase)
        });
    }

    private void CollectFiles(string path, List<string> sourceFiles)
    {
        try
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".zip", ".7z", ".rar", ".tar", ".gz" };
            var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f)) || f.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));
            sourceFiles.AddRange(files);
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

        CheckAndApplyMatch(targetEntries, sourceInfo, fileName, folderName, Path.GetFullPath(jsonFilePath));
    }

    private void ProcessArchiveFile(string archiveFilePath, IReadOnlyList<CameraScriptEntry> targetEntries)
    {
        var readerOptions = new ReaderOptions
        {
            ArchiveEncoding = new ArchiveEncoding()
            {
                Default = Encoding.GetEncoding(932)
            }
        };

        try
        {
            using var fileStream = File.OpenRead(archiveFilePath);
            using var archive = ArchiveFactory.OpenArchive(fileStream, readerOptions);
            string archiveFileName = Path.GetFileName(archiveFilePath);

            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory && !string.IsNullOrEmpty(e.Key) && e.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                string fileName = Path.GetFileName(entry.Key) ?? "";

                if (string.IsNullOrEmpty(fileName) ||
                    fileName.Equals("info.dat", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("cinema-video.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var entryStream = entry.OpenEntryStream();
                using var reader = new StreamReader(entryStream);
                var content = reader.ReadToEnd();

                var sourceInfo = ParseCameraScriptInfo(content);
                if (sourceInfo == null) continue;

                string folderName = Path.GetDirectoryName(entry.Key)?.Replace('\\', '/') ?? "";
                
                string fullArchivePath = Path.GetFullPath(archiveFilePath);
                string displayName = string.IsNullOrEmpty(folderName) ? $"{fullArchivePath}\\{fileName}" : $"{fullArchivePath}\\{folderName.Replace('/', '\\')}\\{fileName}";

                CheckAndApplyMatch(targetEntries, sourceInfo, fileName, string.IsNullOrEmpty(folderName) ? archiveFileName : folderName, displayName);
            }
        }
        catch { }
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

            if (target.MovementCount <= 0 || target.ScriptDuration <= 0)
            {
                continue;
            }

            // 3. Match conditions
            if (sourceInfo.MovementCount == target.MovementCount &&
                Math.Abs(sourceInfo.TotalDurationAndDelay - target.ScriptDuration) <= 0.1)
            {
                if (!target.OriginalSourceFiles.Contains(displayName))
                {
                    target.OriginalSourceFiles.Add(displayName);
                }
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

    private static bool AreSameSearchPaths(IReadOnlyList<string>? cachedPaths, IReadOnlyList<string> currentPaths)
    {
        if (cachedPaths == null)
        {
            return currentPaths.Count == 0;
        }

        if (cachedPaths.Count != currentPaths.Count)
        {
            return false;
        }

        for (int index = 0; index < currentPaths.Count; index++)
        {
            if (!string.Equals(NormalizePath(cachedPaths[index]), NormalizePath(currentPaths[index]), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyCachedMatches(IReadOnlyList<CameraScriptEntry> targetEntries, CachedOriginalScriptMatchResult cachedResult)
    {
        foreach (var targetEntry in targetEntries)
        {
            string targetPath = NormalizePath(targetEntry.FullFilePath);
            if (!cachedResult.MatchesByTargetFilePath.TryGetValue(targetPath, out var matches))
            {
                continue;
            }

            foreach (var match in matches)
            {
                if (!targetEntry.OriginalSourceFiles.Contains(match))
                {
                    targetEntry.OriginalSourceFiles.Add(match);
                }
            }
        }
    }

    private static string NormalizePath(string? path)
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
}
