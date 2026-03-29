using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public sealed class SongScriptsMissingBeatmapDownloadService
{
    private readonly BeatSaverApiClient _apiClient;
    private readonly HashSet<string> _unavailableOnBeatSaverMapIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _alreadyLoadedLatestHashMapIds = new(StringComparer.OrdinalIgnoreCase);

    public SongScriptsMissingBeatmapDownloadService(BeatSaverApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public bool IsDownloadBlocked(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            return true;
        }

        return _unavailableOnBeatSaverMapIds.Contains(mapId) ||
            _alreadyLoadedLatestHashMapIds.Contains(mapId);
    }

    public async Task<SongScriptsMissingBeatmapDownloadResult> DownloadMissingBeatmapAsync(
        string mapId,
        CameraSongScriptCompatibleBeatmapIndex beatmapIndex,
        string customLevelsPath)
    {
        string normalizedMapId = NormalizeMapId(mapId);
        if (string.IsNullOrEmpty(normalizedMapId))
        {
            return SongScriptsMissingBeatmapDownloadResult.Failed("譜面IDが空です。");
        }

        try
        {
            BeatSaverApiResponse? response = await _apiClient.GetMapFromApiAsync(normalizedMapId);
            if (response == null)
            {
                _unavailableOnBeatSaverMapIds.Add(normalizedMapId);
                return SongScriptsMissingBeatmapDownloadResult.UnavailableOnBeatSaver(normalizedMapId);
            }

            BeatSaverVersion? latestVersion = response.Versions.FirstOrDefault(version =>
                !string.IsNullOrWhiteSpace(version.Hash) &&
                !string.IsNullOrWhiteSpace(version.DownloadUrl));

            if (latestVersion == null)
            {
                return SongScriptsMissingBeatmapDownloadResult.Failed("BeatSaverで最新版ZIPを取得できませんでした。");
            }

            string latestHash = NormalizeHash(latestVersion.Hash);
            if (!string.IsNullOrEmpty(latestHash) && beatmapIndex.ByHash.ContainsKey(latestHash))
            {
                _alreadyLoadedLatestHashMapIds.Add(normalizedMapId);
                return SongScriptsMissingBeatmapDownloadResult.AlreadyLoadedLatestHash(normalizedMapId);
            }

            byte[] zipBytes = await _apiClient.DownloadBytesAsync(latestVersion.DownloadUrl);
            if (zipBytes.Length == 0)
            {
                return SongScriptsMissingBeatmapDownloadResult.Failed("BeatSaverから空のZIPが返されました。");
            }

            if (string.IsNullOrWhiteSpace(customLevelsPath))
            {
                return SongScriptsMissingBeatmapDownloadResult.Failed("SettingsでCustomLevelsパスを設定して下さい。");
            }

            string targetRootPath = customLevelsPath;

            Directory.CreateDirectory(targetRootPath);
            string extractedFolderPath = await ExtractZipAsync(zipBytes, targetRootPath, BuildFolderName(response));
            return SongScriptsMissingBeatmapDownloadResult.CreateSuccess(normalizedMapId, extractedFolderPath);
        }
        catch (HttpRequestException ex)
        {
            return SongScriptsMissingBeatmapDownloadResult.Failed($"BeatSaver通信エラー: {ex.Message}");
        }
        catch (Exception ex)
        {
            return SongScriptsMissingBeatmapDownloadResult.Failed(ex.Message);
        }
    }

    private static string BuildFolderName(BeatSaverApiResponse response)
    {
        string songName = response.Metadata?.SongName ?? string.Empty;
        string levelAuthorName = response.Metadata?.LevelAuthorName ?? string.Empty;
        string basePath = string.Format(
            CultureInfo.InvariantCulture,
            "{0} ({1} - {2})",
            response.Id ?? string.Empty,
            songName,
            levelAuthorName);

        return string.Join(
            string.Empty,
            basePath.Split(Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToArray()));
    }

    private static async Task<string> ExtractZipAsync(byte[] zipBytes, string customLevelsPath, string folderName, bool overwrite = false)
    {
        using Stream zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        string path = Path.Combine(customLevelsPath, folderName);
        if (!overwrite && Directory.Exists(path))
        {
            int pathNum = 1;
            while (Directory.Exists(path + $" ({pathNum})"))
            {
                pathNum++;
            }

            path += $" ({pathNum})";
        }

        Directory.CreateDirectory(path);

        await Task.Run(() =>
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name != entry.FullName)
                {
                    continue;
                }

                string entryPath = Path.Combine(path, entry.Name);
                if (overwrite || !File.Exists(entryPath))
                {
                    entry.ExtractToFile(entryPath, overwrite);
                }
            }
        });

        return path;
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
}

public sealed class SongScriptsMissingBeatmapDownloadResult
{
    public bool Success { get; private init; }
    public bool IsUnavailableOnBeatSaver { get; private init; }
    public bool IsAlreadyLoadedLatestHash { get; private init; }
    public string MapId { get; private init; } = "";
    public string ExtractedFolderPath { get; private init; } = "";
    public string ErrorMessage { get; private init; } = "";

    public static SongScriptsMissingBeatmapDownloadResult CreateSuccess(string mapId, string extractedFolderPath)
    {
        return new SongScriptsMissingBeatmapDownloadResult
        {
            Success = true,
            MapId = mapId,
            ExtractedFolderPath = extractedFolderPath
        };
    }

    public static SongScriptsMissingBeatmapDownloadResult UnavailableOnBeatSaver(string mapId)
    {
        return new SongScriptsMissingBeatmapDownloadResult
        {
            MapId = mapId,
            IsUnavailableOnBeatSaver = true,
            ErrorMessage = $"BeatSaverに譜面が見つかりませんでした: {mapId}"
        };
    }

    public static SongScriptsMissingBeatmapDownloadResult AlreadyLoadedLatestHash(string mapId)
    {
        return new SongScriptsMissingBeatmapDownloadResult
        {
            MapId = mapId,
            IsAlreadyLoadedLatestHash = true,
            ErrorMessage = $"最新hashの譜面は既に存在します: {mapId}"
        };
    }

    public static SongScriptsMissingBeatmapDownloadResult Failed(string errorMessage)
    {
        return new SongScriptsMissingBeatmapDownloadResult
        {
            ErrorMessage = errorMessage
        };
    }
}
