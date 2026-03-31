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
            AddSongIfValid(
                playlist.Songs,
                entry.Hash,
                entry.MapId,
                entry.SongName,
                entry.LevelAuthorName);
        }

        WritePlaylist(savePath, playlist);
    }

    public static void ExportToBplist(
        string savePath,
        string title,
        string author,
        string description,
        string coverImagePath,
        IEnumerable<SongScriptsManagerEntry> entries)
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
                var base64 = Convert.ToBase64String(bytes);

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
            }
        }

        var matchedFolderHashCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            AddSongIfValid(
                playlist.Songs,
                ResolveSongScriptsEntryHash(entry, matchedFolderHashCache),
                entry.MapId,
                entry.SongName,
                entry.LevelAuthorName);
        }

        WritePlaylist(savePath, playlist);
    }

    private static void AddSongIfValid(
        ICollection<LegacyPlaylistSongData> songs,
        string? hash,
        string? mapId,
        string? songName,
        string? levelAuthorName)
    {
        var song = new LegacyPlaylistSongData();
        bool hasValidIdentity = false;

        if (!string.IsNullOrWhiteSpace(hash))
        {
            song.Hash = hash;
            hasValidIdentity = true;
        }

        if (!string.IsNullOrWhiteSpace(mapId))
        {
            song.Key = mapId;
            hasValidIdentity = true;
        }

        if (!hasValidIdentity)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(songName))
            song.SongName = songName;

        if (!string.IsNullOrWhiteSpace(levelAuthorName))
            song.LevelAuthorName = levelAuthorName;

        songs.Add(song);
    }

    private static string ResolveSongScriptsEntryHash(
        SongScriptsManagerEntry entry,
        IDictionary<string, string> matchedFolderHashCache)
    {
        if (!string.IsNullOrWhiteSpace(entry.Hash))
        {
            return entry.Hash;
        }

        string customLevelsHash = ResolveMatchedFolderHash(entry.MatchedCustomLevels, matchedFolderHashCache);
        if (!string.IsNullOrWhiteSpace(customLevelsHash))
        {
            return customLevelsHash;
        }

        return ResolveMatchedFolderHash(entry.MatchedCustomWIPLevels, matchedFolderHashCache);
    }

    private static string ResolveMatchedFolderHash(
        IEnumerable<SongScriptsMatchedBeatmapFolder> matchedFolders,
        IDictionary<string, string> matchedFolderHashCache)
    {
        foreach (var folder in matchedFolders)
        {
            if (string.IsNullOrWhiteSpace(folder.FullPath))
            {
                continue;
            }

            if (!matchedFolderHashCache.TryGetValue(folder.FullPath, out string? hash))
            {
                hash = CalculateBeatmapFolderHash(folder.FullPath);
                matchedFolderHashCache[folder.FullPath] = hash;
            }

            if (!string.IsNullOrWhiteSpace(hash))
            {
                return hash;
            }
        }

        return string.Empty;
    }

    private static string CalculateBeatmapFolderHash(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return string.Empty;
        }

        InfoDatData? infoDat = InfoDatReader.ReadFromFolder(folderPath);
        if (infoDat == null || infoDat.BeatmapFilenames.Count == 0)
        {
            return string.Empty;
        }

        return HashCalculator.CalculateSongHash(folderPath, infoDat);
    }

    private static void WritePlaylist(string savePath, LegacyPlaylistData playlist)
    {
        var json = JsonSerializer.Serialize(playlist, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(savePath, json);
    }
}
