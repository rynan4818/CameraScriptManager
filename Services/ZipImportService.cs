using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public class ZipImportService
{
    private static readonly string[] SkipFileNames = { "General_SongScript.json" };

    public List<SongScriptEntry> ImportFile(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".zip" => ImportZip(filePath),
            ".json" => ImportJson(filePath),
            _ => new List<SongScriptEntry>()
        };
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

    private List<SongScriptEntry> ImportZip(string zipPath)
    {
        var results = new List<SongScriptEntry>();
        string zipFileName = Path.GetFileName(zipPath);

        try
        {
            // Detect best encoding: try UTF-8 first, fallback to cp932
            Encoding encoding = DetectZipEncoding(zipPath);

            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Read, encoding);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // Directory
                if (!entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                if (SkipFileNames.Contains(entry.Name, StringComparer.OrdinalIgnoreCase)) continue;

                try
                {
                    string content;
                    using (var stream = entry.Open())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        content = reader.ReadToEnd();
                    }

                    if (!SongScriptValidator.IsValidSongScript(content))
                        continue;

                    // Extract hex ID from path segments
                    string[] segments = entry.FullName.Replace('\\', '/').Split('/');
                    string? hexId = null;
                    string songName = "";

                    if (entry.Name.Equals("SongScript.json", StringComparison.OrdinalIgnoreCase)
                        || entry.Name.Equals("_SongScript.json", StringComparison.OrdinalIgnoreCase))
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
                        string baseName = Path.GetFileNameWithoutExtension(entry.Name);
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

                    // IDが見つからなくてもリストに追加する（ID欄は空）

                    results.Add(new SongScriptEntry
                    {
                        HexId = hexId ?? "",
                        SongName = songName,
                        SourceSongName = songName,
                        SourceFileName = entry.FullName,
                        SourceZipName = zipFileName,
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
            // Skip invalid zip files
        }

        return results;
    }

    /// <summary>
    /// Zipファイルのエントリ名エンコーディングを検出する。
    /// UTF-8を先に試し、置換文字(U+FFFD)が含まれる場合はcp932にフォールバックする。
    /// </summary>
    private static Encoding DetectZipEncoding(string zipPath)
    {
        try
        {
            using var testArchive = ZipFile.Open(zipPath, ZipArchiveMode.Read, Encoding.UTF8);
            bool hasReplacementChar = testArchive.Entries.Any(e => e.FullName.Contains('\uFFFD'));
            if (!hasReplacementChar)
                return Encoding.UTF8;
        }
        catch { }

        // UTF-8で読めない場合はcp932にフォールバック
        try
        {
            return Encoding.GetEncoding(932);
        }
        catch
        {
            return Encoding.UTF8;
        }
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
