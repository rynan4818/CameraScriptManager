using System.IO;
using System.Text;
using System.Text.Json;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public class SongScriptCopyService
{
    private readonly BeatSaverApiClient _apiClient;

    public SongScriptCopyService(BeatSaverApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<List<CopyResult>> CopyAllAsync(
        IList<SongScriptEntry> entries,
        bool addMetadata,
        bool createBackup,
        string backupRootPath,
        string customLevelsRootPath,
        string customWipLevelsRootPath,
        IProgress<string>? progress = null)
    {
        var results = new List<CopyResult>();

        foreach (var entry in entries)
        {
            string jsonToWrite = addMetadata ? PrepareJson(entry) : entry.JsonContent;

            if (entry.CopyToCustomLevels && entry.SelectedCustomLevelsFolder != null)
            {
                string fileName = GetTargetFileName(entry);
                string targetPath = Path.Combine(entry.SelectedCustomLevelsFolder.FullPath, fileName);
                results.Add(CopyFile(entry, jsonToWrite, targetPath, createBackup, backupRootPath, customLevelsRootPath, "CustomLevels"));
                progress?.Report($"コピー完了: {entry.SelectedCustomLevelsFolder.FolderName}");
            }

            if (entry.CopyToCustomWIPLevels && entry.SelectedCustomWIPLevelsFolder != null)
            {
                string fileName = GetTargetFileName(entry);
                string targetPath = Path.Combine(entry.SelectedCustomWIPLevelsFolder.FullPath, fileName);
                results.Add(CopyFile(entry, jsonToWrite, targetPath, createBackup, backupRootPath, customWipLevelsRootPath, "CustomWIPLevels"));
                progress?.Report($"コピー完了: {entry.SelectedCustomWIPLevelsFolder.FolderName}");
            }
        }

        return Task.FromResult(results);
    }

    private string PrepareJson(SongScriptEntry entry)
    {
        return MetadataService.PrepareJsonWithMetadata(
            entry.JsonContent,
            entry.HexId,
            entry.Hash,
            entry.CameraScriptAuthorName ?? "",
            entry.Bpm,
            entry.Duration,
            entry.SongName,
            entry.SongSubName,
            entry.SongAuthorName,
            entry.LevelAuthorName,
            entry.AvatarHeight,
            entry.Description ?? "");
    }

    public static string GetTargetFileName(SongScriptEntry entry)
    {
        // 手動編集されたファイル名がある場合はそれを優先
        if (!string.IsNullOrEmpty(entry.CustomFileName))
            return SanitizeFileName(entry.CustomFileName);

        if (entry.RenameChoice == RenameOption.カスタム)
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            var tags = new Dictionary<string, string>
            {
                { "MapId", entry.HexId },
                { "SongName", entry.SongName },
                { "SongSubName", entry.SongSubName },
                { "SongAuthorName", entry.SongAuthorName },
                { "LevelAuthorName", entry.LevelAuthorName },
                { "CameraScriptAuthorName", entry.CameraScriptAuthorName ?? "" },
                { "FileName", Path.GetFileName(entry.SourceFileName) },
                { "Bpm", entry.Bpm > 0 ? entry.Bpm.ToString() : "" }
            };
            string name = NamingEngine.ReplaceTags(settings.CopierRenameCustomFormat, tags);
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                name += ".json";
            return SanitizeFileName(name);
        }

        return entry.RenameChoice switch
        {
            RenameOption.無し =>
                SanitizeFileName(Path.GetFileName(entry.SourceFileName)),
            RenameOption.IdAuthorSongName =>
                SanitizeFileName($"{entry.HexId}_{entry.CameraScriptAuthorName}_{entry.SongName}_SongScript.json"),
            _ => "SongScript.json"
        };
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static CopyResult CopyFile(
        SongScriptEntry entry,
        string content,
        string targetPath,
        bool createBackup,
        string backupRootPath,
        string sourceRootPath,
        string backupSubfolderName)
    {
        try
        {
            bool overwrite = File.Exists(targetPath);
            if (overwrite && createBackup)
            {
                string backupPath = GetBackupFilePath(targetPath, backupRootPath, sourceRootPath, backupSubfolderName);
                string? backupDirectory = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrWhiteSpace(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                File.Copy(targetPath, backupPath, overwrite: true);
            }

            File.WriteAllText(targetPath, content, Encoding.UTF8);
            return new CopyResult
            {
                HexId = entry.HexId,
                TargetPath = targetPath,
                Success = true,
                WasOverwrite = overwrite
            };
        }
        catch (Exception ex)
        {
            return new CopyResult
            {
                HexId = entry.HexId,
                TargetPath = targetPath,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public static bool CheckOverwrite(SongScriptEntry entry, BeatMapFolder? folder)
    {
        if (folder == null) return false;
        string fileName = GetTargetFileName(entry);
        string targetPath = Path.Combine(folder.FullPath, fileName);
        return File.Exists(targetPath);
    }

    private static string GetBackupFilePath(string targetPath, string backupRootPath, string sourceRootPath, string backupSubfolderName)
    {
        string copierBackupRoot = Path.Combine(BackupPathResolver.GetCopierBackupDirectory(backupRootPath), backupSubfolderName);
        string relativePath = GetRelativePathUnderRoot(sourceRootPath, targetPath);
        string backupFileName = BackupPathResolver.AppendTimestampToFileName(Path.GetFileName(relativePath), DateTime.Now);
        string? relativeDirectory = Path.GetDirectoryName(relativePath);
        return string.IsNullOrWhiteSpace(relativeDirectory)
            ? Path.Combine(copierBackupRoot, backupFileName)
            : Path.Combine(copierBackupRoot, relativeDirectory, backupFileName);
    }

    private static string GetRelativePathUnderRoot(string sourceRootPath, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(sourceRootPath) || string.IsNullOrWhiteSpace(targetPath))
        {
            return Path.Combine(Path.GetFileName(Path.GetDirectoryName(targetPath) ?? string.Empty), Path.GetFileName(targetPath));
        }

        try
        {
            string fullRoot = Path.GetFullPath(sourceRootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullTarget = Path.GetFullPath(targetPath);

            if (fullTarget.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullTarget, fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(fullRoot, fullTarget);
            }
        }
        catch
        {
        }

        string folderName = Path.GetFileName(Path.GetDirectoryName(targetPath) ?? string.Empty);
        return string.IsNullOrWhiteSpace(folderName)
            ? Path.GetFileName(targetPath)
            : Path.Combine(folderName, Path.GetFileName(targetPath));
    }
}
