using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace CameraScriptManager.Services;

public static class MetadataService
{
    public static string PrepareJsonWithMetadata(
        string originalJson,
        string mapId,
        string cameraScriptAuthorName,
        double bpm,
        double duration,
        string songName,
        string songSubName,
        string songAuthorName,
        string levelAuthorName)
    {
        try
        {
            using var doc = JsonDocument.Parse(originalJson);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            // Write metadata block
            writer.WritePropertyName("metadata");
            writer.WriteStartObject();
            writer.WriteString("mapId", mapId);
            writer.WriteString("cameraScriptAuthorName", cameraScriptAuthorName);
            writer.WriteNumber("bpm", bpm);
            writer.WriteNumber("duration", duration);
            writer.WriteString("songName", songName);
            writer.WriteString("songSubName", songSubName);
            writer.WriteString("songAuthorName", songAuthorName);
            writer.WriteString("levelAuthorName", levelAuthorName);
            writer.WriteEndObject();

            // Copy all other properties, skipping old metadata/legacy fields
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name is "metadata" or "mapId" or "songScriptAuthor" or "cameraScriptAuthor" or "cameraScriptAuthorName")
                    continue;
                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return originalJson;
        }
    }

    public static void CreateBackupAndWriteMetadata(
        IList<(string fullFilePath, string originalJson, string newJson, string sourceType)> files,
        string customLevelsPath,
        string customWIPLevelsPath)
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var backupDir = Path.Combine(exeDir, "backup");
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var zipPath = Path.Combine(backupDir, $"backup_{timestamp}.zip");

        // Create backup zip with all original files
        using (var zipStream = File.Create(zipPath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            foreach (var (fullFilePath, originalJson, _, sourceType) in files)
            {
                string basePath = sourceType == "CustomLevels" ? customLevelsPath : customWIPLevelsPath;
                string baseFolder = sourceType == "CustomLevels" ? "CustomLevels" : "CustomWIPLevels";

                string relativePath;
                if (!string.IsNullOrWhiteSpace(basePath) && fullFilePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = fullFilePath[basePath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                else
                {
                    relativePath = Path.GetFileName(fullFilePath);
                }

                var entryPath = Path.Combine(baseFolder, relativePath).Replace('\\', '/');
                var entry = archive.CreateEntry(entryPath);
                using var entryStream = entry.Open();
                using var sw = new StreamWriter(entryStream, Encoding.UTF8);
                sw.Write(originalJson);
            }
        }

        // Write updated JSON files
        foreach (var (fullFilePath, _, newJson, _) in files)
        {
            File.WriteAllText(fullFilePath, newJson, Encoding.UTF8);
        }
    }
}
