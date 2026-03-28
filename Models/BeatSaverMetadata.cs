using System.Text.Json.Serialization;

namespace CameraScriptManager.Models;

public class BeatSaverApiResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("metadata")]
    public BeatSaverMetadata? Metadata { get; set; }

    [JsonPropertyName("versions")]
    public List<BeatSaverVersion> Versions { get; set; } = new();
}

public class BeatSaverMetadata
{
    [JsonPropertyName("bpm")]
    public double Bpm { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("songName")]
    public string SongName { get; set; } = "";

    [JsonPropertyName("songSubName")]
    public string SongSubName { get; set; } = "";

    [JsonPropertyName("songAuthorName")]
    public string SongAuthorName { get; set; } = "";

    [JsonPropertyName("levelAuthorName")]
    public string LevelAuthorName { get; set; } = "";
}

public class BeatSaverVersion
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";

    [JsonPropertyName("downloadURL")]
    public string DownloadUrl { get; set; } = "";
}
