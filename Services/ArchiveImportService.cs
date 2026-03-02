using System.IO;
using System.Text;
using System.Text.Json;
using CameraScriptManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace CameraScriptManager.Services;

public class ArchiveImportService
{
    private static readonly string[] SkipFileNames = { "General_SongScript.json", "info.dat", "BPMInfo.dat", "cinema-video.json", "AudioData.dat" };
    private static readonly string[] SupportedExtensions = { ".zip", ".7z", ".rar", ".tar", ".tar.gz", ".gz" };

    public List<SongScriptEntry> ImportFile(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Handle .tar.gz specifically if needed, but Path.GetExtension gets the last dot.
        if (filePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            ext = ".tar.gz";

        if (ext == ".json")
        {
            return ImportJson(filePath);
        }
        else if (SupportedExtensions.Contains(ext))
        {
            return ImportArchive(filePath);
        }
        
        return new List<SongScriptEntry>();
    }

    private List<SongScriptEntry> ImportJson(string filePath)
    {
        var results = new List<SongScriptEntry>();
        try
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            if (!SongScriptValidator.IsValidSongScript(content))
                return results;

            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string? hexId = HexIdExtractor.ExtractHexId(baseName);
            string songName = hexId != null ? HexIdExtractor.ExtractSongName(baseName) : baseName;

            results.Add(new SongScriptEntry
            {
                HexId = hexId ?? "",
                SongName = songName,
                SourceSongName = songName,
                SourceFileName = Path.GetFileName(filePath),
                SourceZipName = null,
                JsonContent = content,
                ScriptDuration = CalculateScriptDuration(content)
            });
        }
        catch
        {
            // Skip unreadable files
        }
        return results;
    }

    private List<SongScriptEntry> ImportArchive(string archivePath)
    {
        var results = new List<SongScriptEntry>();
        string archiveFileName = Path.GetFileName(archivePath);

        try
        {
            var readerOptions = new ReaderOptions
            {
                ArchiveEncoding = new ArchiveEncoding()
                {
                    Default = Encoding.GetEncoding(932)
                }
            };

            using var fileStream = File.OpenRead(archivePath);
            using var archive = ArchiveFactory.OpenArchive(fileStream, readerOptions);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory || string.IsNullOrEmpty(entry.Key)) continue;
                if (!entry.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                
                string fileName = Path.GetFileName(entry.Key);
                if (SkipFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase)) continue;

                try
                {
                    string content;
                    using (var entryStream = entry.OpenEntryStream())
                    using (var reader = new StreamReader(entryStream))
                    {
                        content = reader.ReadToEnd();
                    }

                    if (!SongScriptValidator.IsValidSongScript(content))
                        continue;

                    // Extract hex ID from path segments
                    string keyStr = entry.Key ?? "";
                    string[] segments = keyStr.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    string? hexId = null;
                    string songName = "";

                    if (fileName.Equals("SongScript.json", StringComparison.OrdinalIgnoreCase)
                        || fileName.Equals("_SongScript.json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Look at parent folder names (innermost first)
                        for (int i = segments.Length - 2; i >= 0; i--)
                        {
                            hexId = HexIdExtractor.ExtractHexId(segments[i]);
                            if (hexId != null)
                            {
                                songName = HexIdExtractor.ExtractSongName(segments[i]);
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Renamed JSON file
                        string baseName = Path.GetFileNameWithoutExtension(fileName);
                        hexId = HexIdExtractor.ExtractHexId(baseName);
                        if (hexId != null)
                        {
                            songName = HexIdExtractor.ExtractSongName(baseName);
                        }
                        else
                        {
                            // Fallback: look at parent folders
                            for (int i = segments.Length - 2; i >= 0; i--)
                            {
                                hexId = HexIdExtractor.ExtractHexId(segments[i]);
                                if (hexId != null)
                                {
                                    songName = HexIdExtractor.ExtractSongName(segments[i]);
                                    break;
                                }
                            }
                        }
                    }

                    results.Add(new SongScriptEntry
                    {
                        HexId = hexId ?? "",
                        SongName = songName,
                        SourceSongName = songName,
                        SourceFileName = entry.Key ?? "",
                        SourceZipName = archiveFileName,
                        JsonContent = content,
                        ScriptDuration = CalculateScriptDuration(content)
                    });
                }
                catch
                {
                    // Skip unreadable entries
                }
            }
        }
        catch
        {
            // Skip invalid archive files
        }

        return results;
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
