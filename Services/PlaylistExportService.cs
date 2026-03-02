using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public static class PlaylistExportService
{
    // BeatSaberPlaylistsLib LegacyPlaylist (.bplist) definition mapping
    private class LegacyPlaylistData
    {
        [JsonPropertyName("playlistTitle")]
        public string PlaylistTitle { get; set; } = "";

        [JsonPropertyName("playlistAuthor")]
        public string? PlaylistAuthor { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("playlistDescription")]
        public string? PlaylistDescription { get; set; }

        [JsonPropertyName("image")]
        public string Image { get; set; } = ""; // base64

        [JsonPropertyName("songs")]
        public List<LegacyPlaylistSongData> Songs { get; set; } = new();
    }

    private class LegacyPlaylistSongData
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("hash")]
        public string? Hash { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("songName")]
        public string? SongName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("levelAuthorName")]
        public string? LevelAuthorName { get; set; }
    }

    public static void ExportToBplist(
        string savePath,
        string title,
        string author,
        string description,
        string coverImagePath,
        IEnumerable<CameraScriptEntry> entries)
    {
        var playlist = new LegacyPlaylistData
        {
            PlaylistTitle = title,
            PlaylistAuthor = string.IsNullOrWhiteSpace(author) ? null : author,
            PlaylistDescription = string.IsNullOrWhiteSpace(description) ? null : description
        };

        if (!string.IsNullOrWhiteSpace(coverImagePath) && File.Exists(coverImagePath))
        {
            try
            {
                var bytes = File.ReadAllBytes(coverImagePath);
                // BeatSaberPlaylistsLib uses standard base64 strings with or without MIME headers.
                // It internally converts base64 via Convert.FromBase64String.
                var base64 = Convert.ToBase64String(bytes);
                
                // Add MIME type prefix for good measure, though BeatSaberPlaylistsLib can handle raw base64.
                var ext = Path.GetExtension(coverImagePath).ToLowerInvariant();
                var mimeType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    _ => "image/png"
                };
                playlist.Image = $"data:{mimeType};base64,{base64}";
            }
            catch
            {
                // Ignoring image load failures to not block playlist creation
            }
        }

        foreach (var entry in entries)
        {
            var song = new LegacyPlaylistSongData();
            
            // Prefer Hash, then Key
            bool hasValidIdentity = false;
            
            if (!string.IsNullOrWhiteSpace(entry.Hash))
            {
                song.Hash = entry.Hash;
                hasValidIdentity = true;
            }

            if (!string.IsNullOrWhiteSpace(entry.MapId))
            {
                song.Key = entry.MapId;
                hasValidIdentity = true;
            }

            if (!hasValidIdentity)
            {
                // Skip entries that don't have a hash or a key, 
                // as they cannot be reliably identified in the game.
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.SongName))
                song.SongName = entry.SongName;

            if (!string.IsNullOrWhiteSpace(entry.LevelAuthorName))
                song.LevelAuthorName = entry.LevelAuthorName;

            playlist.Songs.Add(song);
        }

        var json = JsonSerializer.Serialize(playlist, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(savePath, json);
    }
}
