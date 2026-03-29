using System.IO;
using System.Text;
using System.Text.Json;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public static class SongScriptsMetadataJsonService
{
    public static string PrepareJsonWithMetadata(SongScriptsManagerEntry entry, string originalJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(originalJson);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WritePropertyName("metadata");
            writer.WriteStartObject();
            writer.WriteString("mapId", entry.MapId ?? "");
            writer.WriteString("hash", entry.Hash ?? "");
            writer.WriteString("cameraScriptAuthorName", entry.CameraScriptAuthorName ?? "");
            writer.WriteNumber("bpm", entry.Bpm);
            writer.WriteNumber("duration", entry.Duration);
            writer.WriteString("songName", entry.SongName ?? "");
            writer.WriteString("songSubName", entry.SongSubName ?? "");
            writer.WriteString("songAuthorName", entry.SongAuthorName ?? "");
            writer.WriteString("levelAuthorName", entry.LevelAuthorName ?? "");
            writer.WriteNumber("avatarHeight", entry.AvatarHeight);
            writer.WriteString("description", entry.Description ?? "");
            writer.WriteEndObject();

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Name is "metadata" or "mapId" or "hash" or "songScriptAuthor" or "cameraScriptAuthor" or "cameraScriptAuthorName")
                    continue;

                property.WriteTo(writer);
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
}
