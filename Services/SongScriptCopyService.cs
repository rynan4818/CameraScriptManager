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

    public async Task<List<CopyResult>> CopyAllAsync(
        IList<SongScriptEntry> entries,
        bool addMetadata,
        IProgress<string>? progress = null)
    {
        var results = new List<CopyResult>();

        foreach (var entry in entries)
        {
            if (addMetadata && entry.Metadata == null)
            {
                progress?.Report($"API取得中: {entry.HexId}...");
                var apiResponse = await _apiClient.GetMapAsync(entry.HexId);
                entry.Metadata = apiResponse?.Metadata;
                await Task.Delay(200); // Rate limiting
            }

            string jsonToWrite = addMetadata ? PrepareJson(entry) : entry.JsonContent;

            if (entry.CopyToCustomLevels && entry.SelectedCustomLevelsFolder != null)
            {
                string fileName = GetTargetFileName(entry);
                string targetPath = Path.Combine(entry.SelectedCustomLevelsFolder.FullPath, fileName);
                results.Add(CopyFile(entry, jsonToWrite, targetPath));
                progress?.Report($"コピー完了: {entry.SelectedCustomLevelsFolder.FolderName}");
            }

            if (entry.CopyToCustomWIPLevels && entry.SelectedCustomWIPLevelsFolder != null)
            {
                string fileName = GetTargetFileName(entry);
                string targetPath = Path.Combine(entry.SelectedCustomWIPLevelsFolder.FullPath, fileName);
                results.Add(CopyFile(entry, jsonToWrite, targetPath));
                progress?.Report($"コピー完了: {entry.SelectedCustomWIPLevelsFolder.FolderName}");
            }
        }

        return results;
    }

    private string PrepareJson(SongScriptEntry entry)
    {
        try
        {
            using var doc = JsonDocument.Parse(entry.JsonContent);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            if (entry.Metadata != null)
            {
                writer.WritePropertyName("metadata");
                writer.WriteStartObject();
                writer.WriteString("mapId", entry.HexId);
                writer.WriteString("cameraScriptAuthorName", entry.CameraScriptAuthorName ?? "");
                writer.WriteNumber("bpm", entry.Metadata.Bpm);
                writer.WriteNumber("duration", entry.OggDuration > 0 ? entry.OggDuration : entry.Metadata.Duration);
                writer.WriteString("songName", entry.Metadata.SongName ?? "");
                writer.WriteString("songSubName", entry.Metadata.SongSubName ?? "");
                writer.WriteString("songAuthorName", entry.Metadata.SongAuthorName ?? "");
                writer.WriteString("levelAuthorName", entry.Metadata.LevelAuthorName ?? "");
                writer.WriteEndObject();
            }
            else
            {
                // metadataがない場合でもmapIdとcameraScriptAuthorNameはmetadata内に書く
                writer.WritePropertyName("metadata");
                writer.WriteStartObject();
                writer.WriteString("mapId", entry.HexId);
                writer.WriteString("cameraScriptAuthorName", entry.CameraScriptAuthorName ?? "");
                if (entry.OggDuration > 0)
                    writer.WriteNumber("duration", entry.OggDuration);
                writer.WriteEndObject();
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name is "mapId" or "songScriptAuthor" or "cameraScriptAuthor" or "cameraScriptAuthorName" or "metadata")
                    continue;
                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return entry.JsonContent;
        }
    }

    public static string GetTargetFileName(SongScriptEntry entry)
    {
        // 手動編集されたファイル名がある場合はそれを優先
        if (!string.IsNullOrEmpty(entry.CustomFileName))
            return SanitizeFileName(entry.CustomFileName);

        return entry.RenameChoice switch
        {
            RenameOption.None =>
                SanitizeFileName(Path.GetFileName(entry.SourceFileName)),
            RenameOption.AuthorIdSongName =>
                SanitizeFileName($"{entry.CameraScriptAuthorName}_{entry.HexId}_{entry.SongName}_SongScript.json"),
            _ => "SongScript.json"
        };
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static CopyResult CopyFile(SongScriptEntry entry, string content, string targetPath)
    {
        try
        {
            bool overwrite = File.Exists(targetPath);
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
}
