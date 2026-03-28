using System.IO;
using System.Text.Json;

namespace CameraScriptManager.Services;

public class InfoDatData
{
    public string InfoDatPath { get; set; } = "";
    public string RawInfoDatContent { get; set; } = "";
    public bool IsVersion4 { get; set; }
    public string SongName { get; set; } = "";
    public string SongSubName { get; set; } = "";
    public string SongAuthorName { get; set; } = "";
    public string LevelAuthorName { get; set; } = "";
    public double Bpm { get; set; }
    public string SongFileName { get; set; } = "";
    public string AudioDataFileName { get; set; } = "";
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
            var data = new InfoDatData
            {
                InfoDatPath = infoDatPath,
                RawInfoDatContent = json
            };

            // Detect version
            string version = "";
            if (root.TryGetProperty("_version", out var v2ver))
                version = v2ver.GetString() ?? "";
            else if (root.TryGetProperty("version", out var v4ver))
                version = v4ver.GetString() ?? "";

            if (version.StartsWith("4"))
            {
                data.IsVersion4 = true;
                ReadV4(root, data);
            }
            else
            {
                ReadV2(root, data);
            }

            return data;
        }
        catch
        {
            return null;
        }
    }

    private static void ReadV2(JsonElement root, InfoDatData data)
    {
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

        if (root.TryGetProperty("_songFilename", out var songFileName))
            data.SongFileName = songFileName.GetString() ?? "";

        // Preserve SongCore hash input order, including duplicates.
        if (root.TryGetProperty("_difficultyBeatmapSets", out var sets) && sets.ValueKind == JsonValueKind.Array)
        {
            var filenames = new List<string>();
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
            data.BeatmapFilenames = filenames;
        }
    }

    private static void ReadV4(JsonElement root, InfoDatData data)
    {
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

            if (audio.TryGetProperty("songFilename", out var songFilename))
                data.SongFileName = songFilename.GetString() ?? "";

            if (audio.TryGetProperty("audioDataFilename", out var audioDataFilename))
                data.AudioDataFileName = audioDataFilename.GetString() ?? "";
        }

        var filenames = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.AudioDataFileName))
            filenames.Add(data.AudioDataFileName);

        // Preserve SongCore hash input order, including duplicates.
        if (root.TryGetProperty("difficultyBeatmaps", out var diffs) && diffs.ValueKind == JsonValueKind.Array)
        {
            var mappers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        }
        data.BeatmapFilenames = filenames;
    }
}
