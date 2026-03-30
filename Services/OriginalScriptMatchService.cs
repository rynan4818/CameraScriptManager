using System.IO;
using System.Text;
using System.Text.Json;
using System.Globalization;
using CameraScriptManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace CameraScriptManager.Services;

public class OriginalScriptMatchService
{
    private const int CurrentMatchAlgorithmVersion = 3;
    private const double DurationToleranceSeconds = 0.1;
    private const double ModeBucketScale = 10.0;
    private const string DebugLogFileName = "debug_original_script_match.log";
    private static readonly object DebugLogSyncRoot = new();
    private readonly string[] _searchPaths;
    private readonly Action<string, double?>? _progressCallback;
    private readonly SearchCacheService _searchCacheService = new();
    private int _comparisonCount;
    private int _matchedComparisonCount;
    private int _addedMatchCount;

    public OriginalScriptMatchService(IEnumerable<string> searchPaths, Action<string, double?>? progressCallback = null)
    {
        _searchPaths = searchPaths.Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p)).ToArray();
        _progressCallback = progressCallback;
    }

    public async Task MatchOriginalScriptsAsync(IReadOnlyList<CameraScriptEntry> targetEntries)
    {
        _comparisonCount = 0;
        _matchedComparisonCount = 0;
        _addedMatchCount = 0;

        string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        DebugLog($"===== OriginalScriptMatch START session={sessionId} =====");
        DebugLog($"session={sessionId} searchPaths={FormatPathList(_searchPaths)} targetCount={targetEntries.Count}");

        foreach (var targetEntry in targetEntries)
        {
            targetEntry.OriginalSourceFiles.Clear();
        }

        if (_searchPaths.Length == 0 || targetEntries.Count == 0)
        {
            DebugLog($"session={sessionId} skipped reason=no_search_paths_or_targets validSearchPathCount={_searchPaths.Length} targetCount={targetEntries.Count}");
            DebugLog($"===== OriginalScriptMatch END session={sessionId} =====");
            return;
        }

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
        bool sameAlgorithmVersion = cachedResult.MatchAlgorithmVersion == CurrentMatchAlgorithmVersion;
        bool sameSearchPaths = AreSameSearchPaths(cachedResult.SearchPaths, _searchPaths);
        bool sameSourceFileStamps = SearchCacheService.AreSameFileStamps(cachedResult.SourceFiles, sourceFileStamps);
        bool sameTargetFileStamps = SearchCacheService.AreSameFileStamps(cachedResult.TargetFiles, targetFileStamps);

        if (sameAlgorithmVersion &&
            sameSearchPaths &&
            sameSourceFileStamps &&
            sameTargetFileStamps)
        {
            DebugLog($"session={sessionId} cache=hit algorithmVersion={cachedResult.MatchAlgorithmVersion} sourceFileCount={sourceFiles.Count} targetFileCount={targetEntries.Count}");
            _progressCallback?.Invoke("元データ照合キャッシュを適用中...", 100);
            ApplyCachedMatches(targetEntries, cachedResult);
            int cachedTargetMatchCount = targetEntries.Count(entry => entry.OriginalSourceFiles.Count > 0);
            DebugLog($"session={sessionId} summary comparisons=0 matchedComparisons=0 addedMatches=0 matchedTargets={cachedTargetMatchCount}/{targetEntries.Count} note=cache_applied");
            DebugLog($"===== OriginalScriptMatch END session={sessionId} =====");
            return;
        }

        DebugLog(
            $"session={sessionId} cache=miss " +
            $"algorithmVersion={cachedResult.MatchAlgorithmVersion}/{CurrentMatchAlgorithmVersion} sameAlgorithm={sameAlgorithmVersion} " +
            $"sameSearchPaths={sameSearchPaths} sameSourceFiles={sameSourceFileStamps} sameTargetFiles={sameTargetFileStamps} " +
            $"sourceFileCount={sourceFiles.Count} targetFileCount={targetEntries.Count}");

        List<TargetScriptCandidate> targetCandidates = targetEntries
            .Select(entry => new TargetScriptCandidate(entry, CreateTargetScriptInfo(entry)))
            .ToList();

        foreach (var target in targetCandidates)
        {
            DebugLog($"session={sessionId} target path=\"{target.Entry.FullFilePath}\" metrics={FormatScriptMatchInfo(target.Info)}");
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
                        ProcessArchiveFile(sessionId, sourceFile, targetCandidates);
                    }
                    else
                    {
                        ProcessJsonFile(sessionId, sourceFile, targetCandidates);
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"session={sessionId} sourceError path=\"{sourceFile}\" exception=\"{ex.GetType().Name}: {ex.Message}\"");
                }
            });
        }

        _searchCacheService.SetOriginalScriptMatch(new CachedOriginalScriptMatchResult
        {
            MatchAlgorithmVersion = CurrentMatchAlgorithmVersion,
            SearchPaths = _searchPaths.Select(NormalizePath).ToList(),
            SourceFiles = sourceFileStamps,
            TargetFiles = targetFileStamps,
            MatchesByTargetFilePath = targetEntries.ToDictionary(
                target => NormalizePath(target.FullFilePath),
                target => target.OriginalSourceFiles.ToList(),
                StringComparer.OrdinalIgnoreCase)
        });

        int matchedTargetCount = targetEntries.Count(entry => entry.OriginalSourceFiles.Count > 0);
        DebugLog(
            $"session={sessionId} summary comparisons={_comparisonCount} matchedComparisons={_matchedComparisonCount} " +
            $"addedMatches={_addedMatchCount} matchedTargets={matchedTargetCount}/{targetEntries.Count} sourceFileCount={sourceFiles.Count}");
        DebugLog($"===== OriginalScriptMatch END session={sessionId} =====");
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

    private void ProcessJsonFile(string sessionId, string jsonFilePath, IReadOnlyList<TargetScriptCandidate> targetCandidates)
    {
        var content = File.ReadAllText(jsonFilePath);
        var sourceInfo = ParseCameraScriptInfo(content);
        if (sourceInfo == null)
        {
            DebugLog($"session={sessionId} source path=\"{jsonFilePath}\" type=json metrics=invalid");
            return;
        }

        string displayName = Path.GetFullPath(jsonFilePath);
        DebugLog($"session={sessionId} source path=\"{displayName}\" type=json metrics={FormatScriptMatchInfo(sourceInfo)}");
        CheckAndApplyMatch(sessionId, targetCandidates, sourceInfo, displayName);
    }

    private void ProcessArchiveFile(string sessionId, string archiveFilePath, IReadOnlyList<TargetScriptCandidate> targetCandidates)
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
                string folderName = Path.GetDirectoryName(entry.Key)?.Replace('\\', '/') ?? "";

                string fullArchivePath = Path.GetFullPath(archiveFilePath);
                string displayName = string.IsNullOrEmpty(folderName) ? $"{fullArchivePath}\\{fileName}" : $"{fullArchivePath}\\{folderName.Replace('/', '\\')}\\{fileName}";

                if (sourceInfo == null)
                {
                    DebugLog($"session={sessionId} source path=\"{displayName}\" type=archive_json metrics=invalid");
                    continue;
                }

                DebugLog($"session={sessionId} source path=\"{displayName}\" type=archive_json metrics={FormatScriptMatchInfo(sourceInfo)}");

                CheckAndApplyMatch(sessionId, targetCandidates, sourceInfo, displayName);
            }
        }
        catch (Exception ex)
        {
            DebugLog($"session={sessionId} archiveError path=\"{archiveFilePath}\" exception=\"{ex.GetType().Name}: {ex.Message}\"");
        }
    }

    private void CheckAndApplyMatch(
        string sessionId,
        IReadOnlyList<TargetScriptCandidate> targetCandidates,
        ScriptMatchInfo sourceInfo,
        string displayName)
    {
        foreach (var target in targetCandidates)
        {
            MatchEvaluation evaluation = EvaluateMatch(sourceInfo, target.Info);
            _comparisonCount++;

            if (evaluation.OverallMatch)
            {
                _matchedComparisonCount++;
            }

            DebugLog(BuildComparisonLogLine(sessionId, displayName, target.Entry.FullFilePath, evaluation));

            if (!evaluation.OverallMatch)
            {
                continue;
            }

            if (!target.Entry.OriginalSourceFiles.Contains(displayName))
            {
                target.Entry.OriginalSourceFiles.Add(displayName);
                _addedMatchCount++;
            }
        }
    }

    private static MatchEvaluation EvaluateMatch(ScriptMatchInfo sourceInfo, ScriptMatchInfo? targetInfo)
    {
        if (targetInfo == null)
        {
            return MatchEvaluation.CreateSkipped("target_metrics_missing");
        }

        MetricComparison totalComparison = CompareMetric(sourceInfo.TotalDurationAndDelay, targetInfo.TotalDurationAndDelay);
        MetricComparison secondLargestComparison = CompareMetric(sourceInfo.SecondLargestDuration, targetInfo.SecondLargestDuration);
        MetricComparison secondSmallestComparison = CompareMetric(sourceInfo.SecondSmallestDuration, targetInfo.SecondSmallestDuration);
        MetricComparison medianComparison = CompareMetric(sourceInfo.MedianDuration, targetInfo.MedianDuration);
        MetricComparison modeComparison = CompareMetric(sourceInfo.ModeDuration, targetInfo.ModeDuration);
        bool movementCountMatches = sourceInfo.MovementCount == targetInfo.MovementCount;
        bool targetEligible = targetInfo.MovementCount > 0 && targetInfo.TotalDurationAndDelay > 0;
        bool overallMatch = targetEligible &&
            movementCountMatches &&
            totalComparison.IsMatch &&
            secondLargestComparison.IsMatch &&
            secondSmallestComparison.IsMatch &&
            medianComparison.IsMatch &&
            modeComparison.IsMatch;

        return new MatchEvaluation(
            targetEligible ? null : "target_metrics_invalid",
            sourceInfo.MovementCount,
            targetInfo.MovementCount,
            movementCountMatches,
            totalComparison,
            secondLargestComparison,
            secondSmallestComparison,
            medianComparison,
            modeComparison,
            overallMatch);
    }

    private static ScriptMatchInfo? CreateTargetScriptInfo(CameraScriptEntry entry)
    {
        if (entry.MovementCount <= 0 || entry.ScriptDuration <= 0)
        {
            return null;
        }

        return new ScriptMatchInfo
        {
            MovementCount = entry.MovementCount,
            TotalDurationAndDelay = entry.ScriptDuration,
            SecondLargestDuration = entry.SecondLargestDuration,
            SecondSmallestDuration = entry.SecondSmallestDuration,
            MedianDuration = entry.MedianDuration,
            ModeDuration = entry.ModeDuration
        };
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
            var durations = new List<double>();

            foreach (var movement in movements.EnumerateArray())
            {
                count++;
                double duration = 0;
                if (movement.TryGetProperty("Duration", out var durationInfo))
                {
                    duration = durationInfo.GetDouble();
                }
                durations.Add(duration);
                totalTime += duration;
                if (movement.TryGetProperty("Delay", out var delayInfo))
                {
                    totalTime += delayInfo.GetDouble();
                }
            }

            durations.Sort();

            return new ScriptMatchInfo
            {
                MovementCount = count,
                TotalDurationAndDelay = totalTime,
                SecondLargestDuration = GetSecondLargestDuration(durations),
                SecondSmallestDuration = GetSecondSmallestDuration(durations),
                MedianDuration = GetMedianDuration(durations),
                ModeDuration = GetModeDuration(durations)
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool IsWithinTolerance(double left, double right)
    {
        return Math.Abs(left - right) <= DurationToleranceSeconds;
    }

    private static bool IsWithinTolerance(double? left, double? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return left == right;
        }

        return IsWithinTolerance(left.Value, right.Value);
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

        var grouped = durations
            .Select(duration => (int)Math.Round(duration * ModeBucketScale, MidpointRounding.AwayFromZero))
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

        return grouped[0].Bucket / ModeBucketScale;
    }

    private static MetricComparison CompareMetric(double left, double right)
    {
        double difference = Math.Abs(left - right);
        return new MetricComparison(left, right, difference, difference <= DurationToleranceSeconds);
    }

    private static MetricComparison CompareMetric(double? left, double? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return new MetricComparison(left, right, null, left == right);
        }

        return CompareMetric(left.Value, right.Value);
    }

    private static string BuildComparisonLogLine(string sessionId, string sourcePath, string targetPath, MatchEvaluation evaluation)
    {
        return
            $"session={sessionId} compare source=\"{sourcePath}\" target=\"{targetPath}\" " +
            $"overall={(evaluation.OverallMatch ? "match" : "miss")} reason={evaluation.Reason} " +
            $"movement={evaluation.SourceMovementCount}/{evaluation.TargetMovementCount} pass={FormatBool(evaluation.MovementCountMatches)} " +
            $"total={FormatMetricComparison(evaluation.TotalDurationAndDelayComparison)} " +
            $"secondLargest={FormatMetricComparison(evaluation.SecondLargestDurationComparison)} " +
            $"secondSmallest={FormatMetricComparison(evaluation.SecondSmallestDurationComparison)} " +
            $"median={FormatMetricComparison(evaluation.MedianDurationComparison)} " +
            $"mode={FormatMetricComparison(evaluation.ModeDurationComparison)}";
    }

    private static string FormatMetricComparison(MetricComparison comparison)
    {
        return
            $"{FormatNullableDouble(comparison.SourceValue)}/{FormatNullableDouble(comparison.TargetValue)}" +
            $" diff={FormatNullableDouble(comparison.Difference)} pass={FormatBool(comparison.IsMatch)}";
    }

    private static string FormatScriptMatchInfo(ScriptMatchInfo? info)
    {
        if (info == null)
        {
            return "invalid";
        }

        return
            $"movement={info.MovementCount} " +
            $"total={FormatDouble(info.TotalDurationAndDelay)} " +
            $"secondLargest={FormatNullableDouble(info.SecondLargestDuration)} " +
            $"secondSmallest={FormatNullableDouble(info.SecondSmallestDuration)} " +
            $"median={FormatNullableDouble(info.MedianDuration)} " +
            $"mode={FormatNullableDouble(info.ModeDuration)}";
    }

    private static string FormatPathList(IEnumerable<string> paths)
    {
        string[] values = paths.ToArray();
        return values.Length == 0
            ? "[]"
            : "[" + string.Join(", ", values.Select(path => $"\"{path}\"")) + "]";
    }

    private static string FormatNullableDouble(double? value)
    {
        return value.HasValue ? FormatDouble(value.Value) : "null";
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static void DebugLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] [OriginalMatch] {message}";
        System.Diagnostics.Debug.WriteLine(line);

        try
        {
            lock (DebugLogSyncRoot)
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                File.AppendAllText(Path.Combine(logDir, DebugLogFileName), line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }

    private sealed class TargetScriptCandidate
    {
        public TargetScriptCandidate(CameraScriptEntry entry, ScriptMatchInfo? info)
        {
            Entry = entry;
            Info = info;
        }

        public CameraScriptEntry Entry { get; }

        public ScriptMatchInfo? Info { get; }
    }

    private sealed class MatchEvaluation
    {
        public MatchEvaluation(
            string? reason,
            int sourceMovementCount,
            int targetMovementCount,
            bool movementCountMatches,
            MetricComparison totalDurationAndDelayComparison,
            MetricComparison secondLargestDurationComparison,
            MetricComparison secondSmallestDurationComparison,
            MetricComparison medianDurationComparison,
            MetricComparison modeDurationComparison,
            bool overallMatch)
        {
            Reason = reason ?? (overallMatch ? "all_conditions_passed" : "conditions_not_met");
            SourceMovementCount = sourceMovementCount;
            TargetMovementCount = targetMovementCount;
            MovementCountMatches = movementCountMatches;
            TotalDurationAndDelayComparison = totalDurationAndDelayComparison;
            SecondLargestDurationComparison = secondLargestDurationComparison;
            SecondSmallestDurationComparison = secondSmallestDurationComparison;
            MedianDurationComparison = medianDurationComparison;
            ModeDurationComparison = modeDurationComparison;
            OverallMatch = overallMatch;
        }

        public string Reason { get; }

        public int SourceMovementCount { get; }

        public int TargetMovementCount { get; }

        public bool MovementCountMatches { get; }

        public MetricComparison TotalDurationAndDelayComparison { get; }

        public MetricComparison SecondLargestDurationComparison { get; }

        public MetricComparison SecondSmallestDurationComparison { get; }

        public MetricComparison MedianDurationComparison { get; }

        public MetricComparison ModeDurationComparison { get; }

        public bool OverallMatch { get; }

        public static MatchEvaluation CreateSkipped(string reason)
        {
            MetricComparison empty = new(null, null, null, false);
            return new MatchEvaluation(reason, 0, 0, false, empty, empty, empty, empty, empty, false);
        }
    }

    private readonly record struct MetricComparison(double? SourceValue, double? TargetValue, double? Difference, bool IsMatch);

    private class ScriptMatchInfo
    {
        public int MovementCount { get; set; }
        public double TotalDurationAndDelay { get; set; }
        public double? SecondLargestDuration { get; set; }
        public double? SecondSmallestDuration { get; set; }
        public double? MedianDuration { get; set; }
        public double? ModeDuration { get; set; }
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
