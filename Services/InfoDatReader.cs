using System.IO;
using System.Text.Json;

namespace CameraScriptManager.Services;

public class InfoDatData
{
    public string SongName { get; set; } = "";
    public string SongSubName { get; set; } = "";
    public string SongAuthorName { get; set; } = "";
    public string LevelAuthorName { get; set; } = "";
    public double Bpm { get; set; }
    public List<string> BeatmapFilenames { get; set; } = new();
}

public static class InfoDatReader
{
    private static readonly HashSet<string> _infoDatNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Info.dat",
        "info.dat"
    };

    public static InfoDatData? ReadFromFolder(string folderPath)
    {
        try
        {
            string? infoDatPath = null;
            foreach (var name in _infoDatNames)
            {
                var candidate = Path.Combine(folderPath, name);
                if (File.Exists(candidate))
                {
                    infoDatPath = candidate;
                    break;
                }
            }

            if (infoDatPath == null)
                return null;

            var json = File.ReadAllText(infoDatPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Detect version
            string version = "";
            if (root.TryGetProperty("_version", out var v2ver))
                version = v2ver.GetString() ?? "";
            else if (root.TryGetProperty("version", out var v4ver))
                version = v4ver.GetString() ?? "";

            if (version.StartsWith("4"))
                return ReadV4(root);
            else
                return ReadV2(root);
        }
        catch
        {
            return null;
        }
    }

    private static InfoDatData ReadV2(JsonElement root)
    {
        var data = new InfoDatData();

        if (root.TryGetProperty("_songName", out var songName))
            data.SongName = songName.GetString() ?? "";

        if (root.TryGetProperty("_songSubName", out var songSubName))
            data.SongSubName = songSubName.GetString() ?? "";

        if (root.TryGetProperty("_songAuthorName", out var songAuthor))
            data.SongAuthorName = songAuthor.GetString() ?? "";

        if (root.TryGetProperty("_levelAuthorName", out var levelAuthor))
            data.LevelAuthorName = levelAuthor.GetString() ?? "";

        if (root.TryGetProperty("_beatsPerMinute", out var bpm))
            data.Bpm = bpm.GetDouble();

        // Extract beatmap filenames (v2/v3)
        if (root.TryGetProperty("_difficultyBeatmapSets", out var sets) && sets.ValueKind == JsonValueKind.Array)
        {
            var filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var set in sets.EnumerateArray())
            {
                if (set.TryGetProperty("_difficultyBeatmaps", out var beatmaps) && beatmaps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var diff in beatmaps.EnumerateArray())
                    {
                        if (diff.TryGetProperty("_beatmapFilename", out var filename))
                        {
                            var name = filename.GetString();
                            if (!string.IsNullOrWhiteSpace(name))
                                filenames.Add(name);
                        }
                    }
                }
            }
            data.BeatmapFilenames = filenames.ToList();
        }

        return data;
    }

    private static InfoDatData ReadV4(JsonElement root)
    {
        var data = new InfoDatData();

        if (root.TryGetProperty("song", out var song))
        {
            if (song.TryGetProperty("title", out var title))
                data.SongName = title.GetString() ?? "";

            if (song.TryGetProperty("subTitle", out var subTitle))
                data.SongSubName = subTitle.GetString() ?? "";

            if (song.TryGetProperty("author", out var author))
                data.SongAuthorName = author.GetString() ?? "";
        }

        if (root.TryGetProperty("audio", out var audio))
        {
            if (audio.TryGetProperty("bpm", out var bpm))
                data.Bpm = bpm.GetDouble();
        }

        // levelAuthorName: collect mappers from all difficultyBeatmaps
        if (root.TryGetProperty("difficultyBeatmaps", out var diffs) && diffs.ValueKind == JsonValueKind.Array)
        {
            var mappers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var diff in diffs.EnumerateArray())
            {
                // Mappers
                if (diff.TryGetProperty("beatmapAuthors", out var authors))
                {
                    if (authors.TryGetProperty("mappers", out var mappersArray) && mappersArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var mapper in mappersArray.EnumerateArray())
                        {
                            var name = mapper.GetString();
                            if (!string.IsNullOrWhiteSpace(name))
                                mappers.Add(name);
                        }
                    }
                }

                // Filenames
                if (diff.TryGetProperty("beatmapDataFilename", out var beatmapDataFilename))
                {
                    var name = beatmapDataFilename.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        filenames.Add(name);
                }
                
                if (diff.TryGetProperty("lightshowDataFilename", out var lightshowDataFilename))
                {
                    var name = lightshowDataFilename.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        filenames.Add(name);
                }
            }
            data.LevelAuthorName = string.Join(", ", mappers);
            data.BeatmapFilenames = filenames.ToList();
        }

        return data;
    }
}
