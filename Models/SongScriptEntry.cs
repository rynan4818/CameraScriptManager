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
}
