using System.Text.Json;

namespace CameraScriptManager.Services;

public sealed class CameraScriptMetadataSnapshot
{
    public bool HasMapId { get; init; }
    public string MapId { get; init; } = "";
    public bool HasHash { get; init; }
    public string Hash { get; init; } = "";
    public bool HasCameraScriptAuthorName { get; init; }
    public string CameraScriptAuthorName { get; init; } = "";
    public bool HasSongName { get; init; }
    public string SongName { get; init; } = "";
    public bool HasSongSubName { get; init; }
    public string SongSubName { get; init; } = "";
    public bool HasSongAuthorName { get; init; }
    public string SongAuthorName { get; init; } = "";
    public bool HasLevelAuthorName { get; init; }
    public string LevelAuthorName { get; init; } = "";
    public bool HasBpm { get; init; }
    public double Bpm { get; init; }
    public bool HasDuration { get; init; }
    public double Duration { get; init; }
    public bool HasAvatarHeight { get; init; }
    public double? AvatarHeight { get; init; }
    public bool HasDescription { get; init; }
    public string Description { get; init; } = "";
}

public static class CameraScriptMetadataReader
{
    public static bool TryRead(string jsonContent, out CameraScriptMetadataSnapshot snapshot)
    {
        snapshot = new CameraScriptMetadataSnapshot();

        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            return TryRead(document.RootElement, out snapshot);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryRead(JsonElement root, out CameraScriptMetadataSnapshot snapshot)
    {
        snapshot = new CameraScriptMetadataSnapshot();

        if (!root.TryGetProperty("metadata", out JsonElement metadataElement) ||
            metadataElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        snapshot = new CameraScriptMetadataSnapshot
        {
            HasMapId = metadataElement.TryGetProperty("mapId", out _),
            MapId = ReadString(metadataElement, "mapId"),
            HasHash = metadataElement.TryGetProperty("hash", out _),
            Hash = ReadString(metadataElement, "hash"),
            HasCameraScriptAuthorName = metadataElement.TryGetProperty("cameraScriptAuthorName", out _),
            CameraScriptAuthorName = ReadString(metadataElement, "cameraScriptAuthorName"),
            HasSongName = metadataElement.TryGetProperty("songName", out _),
            SongName = ReadString(metadataElement, "songName"),
            HasSongSubName = metadataElement.TryGetProperty("songSubName", out _),
            SongSubName = ReadString(metadataElement, "songSubName"),
            HasSongAuthorName = metadataElement.TryGetProperty("songAuthorName", out _),
            SongAuthorName = ReadString(metadataElement, "songAuthorName"),
            HasLevelAuthorName = metadataElement.TryGetProperty("levelAuthorName", out _),
            LevelAuthorName = ReadString(metadataElement, "levelAuthorName"),
            HasBpm = metadataElement.TryGetProperty("bpm", out _),
            Bpm = ReadDouble(metadataElement, "bpm"),
            HasDuration = metadataElement.TryGetProperty("duration", out _),
            Duration = ReadDouble(metadataElement, "duration"),
            HasAvatarHeight = metadataElement.TryGetProperty("avatarHeight", out _),
            AvatarHeight = ReadNullableDouble(metadataElement, "avatarHeight"),
            HasDescription = metadataElement.TryGetProperty("description", out _),
            Description = ReadString(metadataElement, "description")
        };

        return true;
    }

    private static string ReadString(JsonElement metadataElement, string propertyName)
    {
        if (!metadataElement.TryGetProperty(propertyName, out JsonElement property))
        {
            return string.Empty;
        }

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
        if (!metadataElement.TryGetProperty(propertyName, out JsonElement property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out double numberValue))
        {
            return numberValue;
        }

        if (property.ValueKind == JsonValueKind.String &&
            double.TryParse(property.GetString(), out double stringValue))
        {
            return stringValue;
        }

        return 0;
    }

    private static double? ReadNullableDouble(JsonElement metadataElement, string propertyName)
    {
        if (!metadataElement.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out double numberValue))
        {
            return numberValue;
        }

        if (property.ValueKind == JsonValueKind.String &&
            double.TryParse(property.GetString(), out double stringValue))
        {
            return stringValue;
        }

        return null;
    }
}
