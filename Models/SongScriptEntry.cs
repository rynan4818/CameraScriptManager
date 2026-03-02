using System.IO;

namespace CameraScriptManager.Models;

public class SongScriptEntry
{
    public string HexId { get; set; } = "";
    public string SongName { get; set; } = "";
    public string SourceSongName { get; set; } = "";
    public SongNameOption SongNameChoice { get; set; } = SongNameOption.Source;
    public string SourceFileName { get; set; } = "";
    public string? SourceZipName { get; set; }
    public string JsonContent { get; set; } = "";
    public string BeatSaverUrl => $"https://beatsaver.com/maps/{HexId}";
    public BeatSaverMetadata? Metadata { get; set; }
    public string CameraScriptAuthorName { get; set; } = "";
    /// <summary>JSONのMovements内のDuration+Delayの合計値（秒）</summary>
    public double ScriptDuration { get; set; }
    /// <summary>譜面フォルダの.egg/.oggファイルから算出したDuration（秒）</summary>
    public double OggDuration { get; set; }
    public List<BeatMapFolder> MatchedCustomLevels { get; set; } = new();
    public List<BeatMapFolder> MatchedCustomWIPLevels { get; set; } = new();
    public bool CopyToCustomLevels { get; set; }
    public bool CopyToCustomWIPLevels { get; set; }
    public BeatMapFolder? SelectedCustomLevelsFolder { get; set; }
    public BeatMapFolder? SelectedCustomWIPLevelsFolder { get; set; }
    public RenameOption RenameChoice { get; set; } = RenameOption.SongScript;
    /// <summary>手動編集されたファイル名（nullの場合はRenameChoiceから自動生成）</summary>
    public string? CustomFileName { get; set; }
    public bool HasOverwriteWarningCustomLevels { get; set; }
    public bool HasOverwriteWarningCustomWIPLevels { get; set; }

    public void UpdateSongName()
    {
        string newName = SourceSongName;
        if (SongNameChoice == SongNameOption.BeatSaverSongName && Metadata != null)
        {
            newName = Metadata.SongName ?? newName;
        }
        else if (SongNameChoice == SongNameOption.BeatSaverSongNameAndAuthor && Metadata != null)
        {
            var song = Metadata.SongName ?? "";
            var author = Metadata.LevelAuthorName ?? "";
            if (!string.IsNullOrEmpty(song) && !string.IsNullOrEmpty(author))
                newName = $"{song} - {author}";
            else if (!string.IsNullOrEmpty(song))
                newName = song;
        }
        SongName = newName;
    }

    public string GenerateRenameDisplayName(string customFormat, string cameraScriptAuthor)
    {
        if (!string.IsNullOrEmpty(CustomFileName))
            return CustomFileName;

        if (RenameChoice == RenameOption.カスタム)
        {
            var tags = new Dictionary<string, string>
            {
                { "MapId", HexId },
                { "SongName", SongName },
                { "SongSubName", Metadata?.SongSubName ?? "" },
                { "SongAuthorName", Metadata?.SongAuthorName ?? "" },
                { "LevelAuthorName", Metadata?.LevelAuthorName ?? "" },
                { "CameraScriptAuthorName", cameraScriptAuthor },
                { "FileName", Path.GetFileName(SourceFileName) },
                { "Bpm", Metadata?.Bpm.ToString() ?? "" }
            };
            string name = Services.NamingEngine.ReplaceTags(customFormat, tags);
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                name += ".json";
            return name;
        }

        return RenameChoice switch
        {
            RenameOption.無し => Path.GetFileName(SourceFileName),
            RenameOption.SongScript => "SongScript.json",
            RenameOption.AuthorIdSongName => $"{cameraScriptAuthor}_{HexId}_{SongName}_SongScript.json",
            _ => "SongScript.json"
        };
    }

    public void UpdateMatchedFolders(
        Dictionary<string, List<BeatMapFolder>> customLevels,
        Dictionary<string, List<BeatMapFolder>> customWIPLevels)
    {
        string key = HexId.ToLowerInvariant();

        MatchedCustomLevels = customLevels.TryGetValue(key, out var clList) ? clList : new();
        MatchedCustomWIPLevels = customWIPLevels.TryGetValue(key, out var wipList) ? wipList : new();

        if (MatchedCustomLevels.Count > 0)
        {
            CopyToCustomLevels = true;
            SelectedCustomLevelsFolder = MatchedCustomLevels[0];
        }
        else
        {
            CopyToCustomLevels = false;
            SelectedCustomLevelsFolder = null;
        }

        if (MatchedCustomWIPLevels.Count > 0)
        {
            CopyToCustomWIPLevels = true;
            SelectedCustomWIPLevelsFolder = MatchedCustomWIPLevels[0];
        }
        else
        {
            CopyToCustomWIPLevels = false;
            SelectedCustomWIPLevelsFolder = null;
        }
    }
}
