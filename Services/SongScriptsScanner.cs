using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public class SongScriptsScanner
{
    public List<SongScriptsManagerEntry> Scan(string songScriptsRootPath, Action<string, double?>? progress = null)
    {
        var results = new List<SongScriptsManagerEntry>();
        if (string.IsNullOrWhiteSpace(songScriptsRootPath) || !Directory.Exists(songScriptsRootPath))
            return results;

        string[] jsonFiles = Directory.GetFiles(songScriptsRootPath, "*.json", SearchOption.AllDirectories);
        string[] zipFiles = Directory.GetFiles(songScriptsRootPath, "*.zip", SearchOption.AllDirectories);

        Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);
        Array.Sort(zipFiles, StringComparer.OrdinalIgnoreCase);

        var sources = new List<(string path, bool isZip)>(jsonFiles.Length + zipFiles.Length);
        sources.AddRange(jsonFiles.Select(path => (path, false)));
        sources.AddRange(zipFiles.Select(path => (path, true)));
        sources = sources
            .OrderBy(source => source.path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            string relativePath = SongScriptsPathResolver.GetRelativePathUnderSongScripts(songScriptsRootPath, source.path);
            double percent = sources.Count == 0 ? 0 : ((index + 1) * 100.0 / sources.Count);
            progress?.Invoke($"SongScripts読込中... {relativePath}", percent);

            if (source.isZip)
                results.AddRange(ScanZipFile(source.path, songScriptsRootPath));
            else
                TryAddJsonEntry(results, source.path, songScriptsRootPath);
        }

        return results
            .OrderBy(entry => entry.SourceDisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void TryAddJsonEntry(ICollection<SongScriptsManagerEntry> results, string filePath, string songScriptsRootPath)
    {
        try
        {
            string jsonContent = File.ReadAllText(filePath);
            if (!IsValidSongScript(jsonContent))
                return;

            string sourceRelativePath = SongScriptsPathResolver.GetRelativePathUnderSongScripts(songScriptsRootPath, filePath);
            if (!TryCreateEntry(jsonContent, filePath, sourceRelativePath, Path.GetFileName(filePath), null, out var entry))
                return;

            results.Add(entry);
        }
        catch
        {
        }
    }

    private IEnumerable<SongScriptsManagerEntry> ScanZipFile(string zipPath, string songScriptsRootPath)
    {
        var results = new List<SongScriptsManagerEntry>();
        string sourceRelativePath = SongScriptsPathResolver.GetRelativePathUnderSongScripts(songScriptsRootPath, zipPath);

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var zipEntry in archive.Entries.OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase))
            {
                if (!zipEntry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (zipEntry.Length == 0)
                    continue;

                try
                {
                    using var stream = zipEntry.Open();
                    using var reader = new StreamReader(stream);
                    string jsonContent = reader.ReadToEnd();
                    if (!IsValidSongScript(jsonContent))
                        continue;

                    if (!TryCreateEntry(
                        jsonContent,
                        zipPath,
                        sourceRelativePath,
                        Path.GetFileName(zipEntry.FullName),
                        zipEntry.FullName,
                        out var entry))
                    {
                        continue;
                    }

                    results.Add(entry);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return results;
    }

    private bool TryCreateEntry(
        string jsonContent,
        string sourceFilePath,
        string sourceRelativePath,
        string fileName,
        string? zipEntryName,
        out SongScriptsManagerEntry entry)
    {
        entry = new SongScriptsManagerEntry();

        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            bool hasMetadataBlock = root.TryGetProperty("metadata", out JsonElement metadataElement)
                && metadataElement.ValueKind == JsonValueKind.Object;

            string metadataMapId = hasMetadataBlock ? NormalizeMapId(ReadString(metadataElement, "mapId")) : string.Empty;
            string metadataHash = hasMetadataBlock ? NormalizeHash(ReadString(metadataElement, "hash")) : string.Empty;
            string pathMapId = ResolvePathMapId(fileName, sourceRelativePath, zipEntryName);

            if (string.IsNullOrEmpty(metadataMapId) &&
                string.IsNullOrEmpty(metadataHash) &&
                string.IsNullOrEmpty(pathMapId))
            {
                return false;
            }

            string displayPath = string.IsNullOrEmpty(zipEntryName)
                ? NormalizeDisplayPath(sourceRelativePath)
                : CombineDisplayPaths(NormalizeDisplayPath(sourceRelativePath), NormalizeDisplayPath(zipEntryName));

            entry = new SongScriptsManagerEntry
            {
                SourceFilePath = sourceFilePath,
                SourceRelativePath = NormalizeDisplayPath(sourceRelativePath),
                ZipEntryName = zipEntryName,
                SourceDisplayPath = displayPath,
                FileName = fileName,
                JsonContent = jsonContent,
                HasMetadataBlock = hasMetadataBlock,
                MetadataMapId = metadataMapId,
                PathMapId = pathMapId,
                MapId = !string.IsNullOrEmpty(metadataMapId) ? metadataMapId : pathMapId,
                Hash = metadataHash,
                CameraScriptAuthorName = hasMetadataBlock ? ReadString(metadataElement, "cameraScriptAuthorName") : string.Empty,
                SongName = hasMetadataBlock ? ReadString(metadataElement, "songName") : string.Empty,
                SongSubName = hasMetadataBlock ? ReadString(metadataElement, "songSubName") : string.Empty,
                SongAuthorName = hasMetadataBlock ? ReadString(metadataElement, "songAuthorName") : string.Empty,
                LevelAuthorName = hasMetadataBlock ? ReadString(metadataElement, "levelAuthorName") : string.Empty,
                Bpm = hasMetadataBlock ? ReadDouble(metadataElement, "bpm") : 0,
                Duration = hasMetadataBlock ? ReadDouble(metadataElement, "duration") : 0,
                AvatarHeight = hasMetadataBlock ? ReadDouble(metadataElement, "avatarHeight") : 0,
                Description = hasMetadataBlock ? ReadString(metadataElement, "description") : string.Empty
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidSongScript(string jsonContent)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            if (!document.RootElement.TryGetProperty("Movements", out var movements))
                return false;

            return movements.ValueKind == JsonValueKind.Array && movements.GetArrayLength() > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadString(JsonElement metadataElement, string propertyName)
    {
        if (!metadataElement.TryGetProperty(propertyName, out var property))
            return string.Empty;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty
        };
    }

    private static double ReadDouble(JsonElement metadataElement, string propertyName)
    {
        if (!metadataElement.TryGetProperty(propertyName, out var property))
            return 0;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out double numberValue))
            return numberValue;

        if (property.ValueKind == JsonValueKind.String &&
            double.TryParse(property.GetString(), out double stringValue))
        {
            return stringValue;
        }

        return 0;
    }

    private static string ResolvePathMapId(string fileName, string sourceRelativePath, string? zipEntryName)
    {
        string fileMapId = ExtractFileMapId(fileName);
        if (!string.IsNullOrEmpty(fileMapId))
            return fileMapId;

        string zipEntryFolderMapId = ExtractFolderMapId(zipEntryName);
        if (!string.IsNullOrEmpty(zipEntryFolderMapId))
            return zipEntryFolderMapId;

        return ExtractFolderMapId(sourceRelativePath);
    }

    private static string ExtractFileMapId(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        return ExtractLeadingMapIdCandidate(Path.GetFileNameWithoutExtension(fileName));
    }

    private static string ExtractFolderMapId(string? path)
    {
        string folderName = GetContainingFolderName(path);
        if (string.IsNullOrEmpty(folderName))
            return string.Empty;

        return ExtractLeadingMapIdCandidate(folderName);
    }

    private static string GetContainingFolderName(string? path)
    {
        string normalizedPath = NormalizeDisplayPath(path);
        if (string.IsNullOrEmpty(normalizedPath))
            return string.Empty;

        string? directoryPath = Path.GetDirectoryName(normalizedPath);
        if (string.IsNullOrEmpty(directoryPath))
            return string.Empty;

        return Path.GetFileName(directoryPath) ?? string.Empty;
    }

    private static string ExtractLeadingMapIdCandidate(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        int length = 0;
        while (length < value.Length && IsHexCharacter(value[length]))
        {
            length++;
        }

        if (length == 0 || length > 6)
            return string.Empty;

        return value[..length].ToLowerInvariant();
    }

    private static bool IsHexCharacter(char value)
    {
        return (value >= '0' && value <= '9') ||
            (value >= 'a' && value <= 'f') ||
            (value >= 'A' && value <= 'F');
    }

    private static string NormalizeMapId(string? mapId)
    {
        return string.IsNullOrWhiteSpace(mapId) ? string.Empty : mapId.Trim().ToLowerInvariant();
    }

    private static string NormalizeHash(string? hash)
    {
        return string.IsNullOrWhiteSpace(hash) ? string.Empty : hash.Trim().ToLowerInvariant();
    }

    private static string NormalizeDisplayPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }

    private static string CombineDisplayPaths(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
            return right;

        if (string.IsNullOrEmpty(right))
            return left;

        return $"{left}{Path.DirectorySeparatorChar}{right}";
    }
}
