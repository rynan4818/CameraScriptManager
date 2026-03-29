using System.IO;
using System.IO.Compression;
using System.Text;

namespace CameraScriptManager.Services;

public static class ZipExportService
{
    public const string PackagingFolderKeepOriginalJson = "FolderKeepOriginalJson";
    public const string PackagingFlatRenameJson = "FlatRenameJson";
    public const string PackagingFolderSongScriptJson = "FolderSongScriptJson";

    public static void Export(
        IList<(string zipEntryFolder, string fileName, string jsonContent)> items,
        string zipFilePath)
    {
        using var zipStream = File.Create(zipFilePath);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        foreach (var (folder, fileName, content) in items)
        {
            string entryPath;
            if (string.IsNullOrWhiteSpace(folder))
                entryPath = fileName;
            else
                entryPath = $"{folder}/{fileName}";

            var entry = archive.CreateEntry(entryPath);
            using var entryStream = entry.Open();
            using var sw = new StreamWriter(entryStream, Encoding.UTF8);
            sw.Write(content);
        }
    }

    public static void ExportToDirectory(
        IList<(string zipEntryFolder, string fileName, string jsonContent)> items,
        string outputDirectoryPath)
    {
        Directory.CreateDirectory(outputDirectoryPath);

        foreach (var (folder, fileName, content) in items)
        {
            string targetDirectory = string.IsNullOrWhiteSpace(folder)
                ? outputDirectoryPath
                : Path.Combine(outputDirectoryPath, folder);

            Directory.CreateDirectory(targetDirectory);

            string targetPath = Path.Combine(targetDirectory, fileName);
            File.WriteAllText(targetPath, content, Encoding.UTF8);
        }
    }

    public static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    public static (string zipEntryFolder, string zipEntryFileName) GetZipEntryParts(
        string packagingMode,
        string namingMode,
        string customFormat,
        CameraScriptManager.Models.CameraScriptEntry entry,
        string cameraScriptAuthor,
        string originalFileName)
    {
        string configuredName = GetConfiguredName(namingMode, customFormat, entry, cameraScriptAuthor);
        string safeOriginalFileName = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(safeOriginalFileName))
        {
            safeOriginalFileName = "SongScript.json";
        }

        return packagingMode switch
        {
            PackagingFlatRenameJson => ("", EnsureJsonFileName(configuredName)),
            PackagingFolderSongScriptJson => (configuredName, "SongScript.json"),
            _ => (configuredName, safeOriginalFileName)
        };
    }

    public static string GetConfiguredName(
        string namingMode,
        string customFormat,
        CameraScriptManager.Models.CameraScriptEntry entry,
        string cameraScriptAuthor)
    {
        if (namingMode == "Custom")
        {
            var tags = new System.Collections.Generic.Dictionary<string, string>
            {
                { "MapId", entry.MapId },
                { "SongName", entry.SongName },
                { "SongSubName", entry.SongSubName },
                { "SongAuthorName", entry.SongAuthorName },
                { "LevelAuthorName", entry.LevelAuthorName },
                { "CameraScriptAuthorName", cameraScriptAuthor },
                { "FileName", Path.GetFileName(entry.FullFilePath) },
                { "Bpm", entry.Bpm.ToString() }
            };
            return NamingEngine.ReplaceTags(customFormat, tags);
        }

        return NamingEngine.SanitizeFileName($"{entry.MapId}_{entry.SongName}_{entry.LevelAuthorName}");
    }

    public static string CreateTimestampedOutputDirectory(string rootDirectoryPath)
    {
        string baseName = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_OUTPUT";
        string candidatePath = Path.Combine(rootDirectoryPath, baseName);
        int suffix = 1;

        while (Directory.Exists(candidatePath))
        {
            candidatePath = Path.Combine(rootDirectoryPath, $"{baseName}_{suffix}");
            suffix++;
        }

        Directory.CreateDirectory(candidatePath);
        return candidatePath;
    }

    private static string EnsureJsonFileName(string name)
    {
        string sanitized = SanitizeFileName(name);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "SongScript.json";
        }

        return sanitized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? sanitized
            : sanitized + ".json";
    }
}
