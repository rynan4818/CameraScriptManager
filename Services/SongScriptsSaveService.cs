using System.IO;
using System.IO.Compression;
using System.Text;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public class SongScriptsSaveService
{
    public Task<List<SongScriptsSaveResult>> SaveAsync(
        IList<SongScriptsManagerEntry> entries,
        string songScriptsRootPath,
        string backupRootPath,
        bool enableBackup,
        IProgress<string>? progress = null)
    {
        return Task.Run(() => Save(entries, songScriptsRootPath, backupRootPath, enableBackup, progress));
    }

    private List<SongScriptsSaveResult> Save(
        IList<SongScriptsManagerEntry> entries,
        string songScriptsRootPath,
        string backupRootPath,
        bool enableBackup,
        IProgress<string>? progress)
    {
        var results = new List<SongScriptsSaveResult>();
        var groups = entries
            .GroupBy(entry => entry.SourceFilePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            var groupEntries = group.ToList();
            string sourceLabel = Path.GetFileName(group.Key);
            progress?.Report($"保存中... {sourceLabel} ({index + 1}/{groups.Count})");

            SongScriptsSaveResult result = groupEntries[0].IsZipEntry
                ? SaveZipGroup(groupEntries, songScriptsRootPath, backupRootPath, enableBackup)
                : SaveJsonEntry(groupEntries[0], songScriptsRootPath, backupRootPath, enableBackup);

            results.Add(result);
        }

        return results;
    }

    private SongScriptsSaveResult SaveJsonEntry(
        SongScriptsManagerEntry entry,
        string songScriptsRootPath,
        string backupRootPath,
        bool enableBackup)
    {
        var result = new SongScriptsSaveResult
        {
            SourceFilePath = entry.SourceFilePath,
            Entries = new List<SongScriptsManagerEntry> { entry }
        };

        try
        {
            BackupSourceFile(entry.SourceFilePath, songScriptsRootPath, backupRootPath, enableBackup);

            string originalJson = LoadJsonFileContent(entry);
            string jsonToWrite = SongScriptsMetadataJsonService.PrepareJsonWithMetadata(entry, originalJson);
            File.WriteAllText(entry.SourceFilePath, jsonToWrite, Encoding.UTF8);
            entry.JsonContent = jsonToWrite;
            entry.HasMetadataBlock = true;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private SongScriptsSaveResult SaveZipGroup(
        IList<SongScriptsManagerEntry> entries,
        string songScriptsRootPath,
        string backupRootPath,
        bool enableBackup)
    {
        string sourceFilePath = entries[0].SourceFilePath;
        var result = new SongScriptsSaveResult
        {
            SourceFilePath = sourceFilePath,
            Entries = entries.ToList()
        };

        string tempFilePath = sourceFilePath + ".tmp";
        try
        {
            BackupSourceFile(sourceFilePath, songScriptsRootPath, backupRootPath, enableBackup);
            TryDeleteTempFile(tempFilePath);

            var pendingEntries = entries.ToDictionary(
                entry => entry.ZipEntryName ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

            using (var sourceArchive = ZipFile.OpenRead(sourceFilePath))
            using (var tempArchive = ZipFile.Open(tempFilePath, ZipArchiveMode.Create))
            {
                foreach (var sourceEntry in sourceArchive.Entries)
                {
                    var destinationEntry = tempArchive.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
                    destinationEntry.LastWriteTime = sourceEntry.LastWriteTime;

                    using var outputStream = destinationEntry.Open();
                    if (pendingEntries.TryGetValue(sourceEntry.FullName, out var managedEntry))
                    {
                        string originalJson = ReadZipEntryContent(sourceEntry);
                        string jsonToWrite = SongScriptsMetadataJsonService.PrepareJsonWithMetadata(managedEntry, originalJson);
                        using var writer = new StreamWriter(outputStream, Encoding.UTF8, 1024, leaveOpen: true);
                        writer.Write(jsonToWrite);
                        writer.Flush();
                        managedEntry.JsonContent = jsonToWrite;
                        managedEntry.HasMetadataBlock = true;
                        pendingEntries.Remove(sourceEntry.FullName);
                    }
                    else if (!string.IsNullOrEmpty(sourceEntry.Name) || sourceEntry.Length > 0)
                    {
                        using var inputStream = sourceEntry.Open();
                        inputStream.CopyTo(outputStream);
                    }
                }
            }

            if (pendingEntries.Count > 0)
            {
                throw new InvalidOperationException($"ZIP内の対象JSONが見つかりませんでした: {string.Join(", ", pendingEntries.Keys)}");
            }

            File.Copy(tempFilePath, sourceFilePath, overwrite: true);
            File.Delete(tempFilePath);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            TryDeleteTempFile(tempFilePath);
        }

        return result;
    }

    private static void BackupSourceFile(string sourceFilePath, string songScriptsRootPath, string backupRootPath, bool enableBackup)
    {
        if (!enableBackup)
        {
            return;
        }

        string backupFilePath = GetBackupFilePath(sourceFilePath, songScriptsRootPath, backupRootPath);
        string? backupDirectory = Path.GetDirectoryName(backupFilePath);
        if (!string.IsNullOrWhiteSpace(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        File.Copy(sourceFilePath, backupFilePath, overwrite: true);
    }

    private static string GetBackupFilePath(string sourceFilePath, string songScriptsRootPath, string backupRootPath)
    {
        string backupDirectoryRoot = BackupPathResolver.GetSongScriptsBackupDirectory(backupRootPath);
        string relativePath = SongScriptsPathResolver.GetRelativePathUnderSongScripts(songScriptsRootPath, sourceFilePath);
        string backupFileName = BackupPathResolver.AppendTimestampToFileName(Path.GetFileName(relativePath), DateTime.Now);
        string? relativeDirectory = Path.GetDirectoryName(relativePath);
        return string.IsNullOrWhiteSpace(relativeDirectory)
            ? Path.Combine(backupDirectoryRoot, backupFileName)
            : Path.Combine(backupDirectoryRoot, relativeDirectory, backupFileName);
    }

    private static void TryDeleteTempFile(string tempFilePath)
    {
        try
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
        catch
        {
        }
    }

    private static string LoadJsonFileContent(SongScriptsManagerEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.JsonContent))
        {
            return entry.JsonContent;
        }

        return File.ReadAllText(entry.SourceFilePath, Encoding.UTF8);
    }

    private static string ReadZipEntryContent(ZipArchiveEntry sourceEntry)
    {
        using var inputStream = sourceEntry.Open();
        using var reader = new StreamReader(inputStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
