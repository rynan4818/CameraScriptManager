using System.Collections.ObjectModel;
using System.IO;
using CameraScriptManager.Models;
using CameraScriptManager.Services;

namespace CameraScriptManager.ViewModels;

public class SongScriptEntryViewModel : ViewModelBase
{
    private readonly SongScriptEntry _model;
    private readonly SettingsService _settingsService = new();

    /// <summary>HexId変更時に呼ばれるコールバック（MainViewModelから設定される）</summary>
    public Func<SongScriptEntryViewModel, Task>? OnHexIdChanged { get; set; }

    public SongScriptEntryViewModel(SongScriptEntry model)
    {
        _model = model;

        CustomLevelsFolders = new ObservableCollection<BeatMapFolder>(model.MatchedCustomLevels);
        CustomWIPLevelsFolders = new ObservableCollection<BeatMapFolder>(model.MatchedCustomWIPLevels);

        _copyToCustomLevels = model.CopyToCustomLevels;
        _copyToCustomWIPLevels = model.CopyToCustomWIPLevels;
        _selectedCustomLevelsFolder = model.SelectedCustomLevelsFolder;
        _selectedCustomWIPLevelsFolder = model.SelectedCustomWIPLevelsFolder;
        _cameraScriptAuthorName = model.CameraScriptAuthorName;
        _renameChoice = model.RenameChoice;
        _hexId = model.HexId;

        UpdateOverwriteWarnings();
    }

    public SongScriptEntry Model => _model;

    // --- Source Display ---
    public string SourceDisplayPath
    {
        get
        {
            if (_model.SourceZipName != null)
                return $"{_model.SourceZipName}/{_model.SourceFileName}";
            return _model.SourceFileName;
        }
    }

    // --- HexId (editable) ---
    private string _hexId = "";
    public string HexId
    {
        get => _hexId;
        set
        {
            if (SetProperty(ref _hexId, value))
            {
                _model.HexId = value;
                OnPropertyChanged(nameof(BeatSaverUrl));
                OnPropertyChanged(nameof(RenameDisplayName));
                _ = OnHexIdChanged?.Invoke(this);
            }
        }
    }

    // --- SongName (updated via API) ---
    private string _songName = "";
    public string SongName
    {
        get => string.IsNullOrEmpty(_songName) ? _model.SongName : _songName;
        set
        {
            if (SetProperty(ref _songName, value))
            {
                _model.SongName = value;
                OnPropertyChanged(nameof(RenameDisplayName));
                UpdateOverwriteWarnings();
            }
        }
    }

    public SongNameOption SongNameChoice
    {
        get => _model.SongNameChoice;
        set
        {
            if (_model.SongNameChoice != value)
            {
                _model.SongNameChoice = value;
                OnPropertyChanged(nameof(SongNameChoice));
                UpdateSongName();
            }
        }
    }

    public void UpdateSongName()
    {
        string newName = _model.SourceSongName;
        if (_model.SongNameChoice == SongNameOption.BeatSaverSongName && _model.Metadata != null)
        {
            newName = _model.Metadata.SongName ?? newName;
        }
        else if (_model.SongNameChoice == SongNameOption.BeatSaverSongNameAndAuthor && _model.Metadata != null)
        {
            var song = _model.Metadata.SongName ?? "";
            var author = _model.Metadata.LevelAuthorName ?? "";
            if (!string.IsNullOrEmpty(song) && !string.IsNullOrEmpty(author))
                newName = $"{song} - {author}";
            else if (!string.IsNullOrEmpty(song))
                newName = song;
        }
        SongName = newName;
    }

    public string BeatSaverUrl => _model.BeatSaverUrl;
    public string? SourceZipName => _model.SourceZipName;

    private string _cameraScriptAuthorName = "";
    public string CameraScriptAuthorName
    {
        get => _cameraScriptAuthorName;
        set
        {
            if (SetProperty(ref _cameraScriptAuthorName, value))
            {
                _model.CameraScriptAuthorName = value;
                OnPropertyChanged(nameof(RenameDisplayName));
                UpdateOverwriteWarnings();
            }
        }
    }

    /// <summary>JSONのMovements内のDuration+Delayの合計値（秒）</summary>
    public double ScriptDuration => _model.ScriptDuration;

    /// <summary>譜面フォルダの音声ファイルから算出した曲のDuration（秒）</summary>
    public double OggDuration => _model.OggDuration;

    public string ScriptDurationText => FormatDuration(ScriptDuration);
    public string OggDurationText => FormatDuration(OggDuration);

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "0:00";
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }

    public void NotifyOggDurationChanged()
    {
        OnPropertyChanged(nameof(OggDuration));
        OnPropertyChanged(nameof(OggDurationText));
    }

    // CustomLevels
    private bool _copyToCustomLevels;
    public bool CopyToCustomLevels
    {
        get => _copyToCustomLevels;
        set
        {
            if (SetProperty(ref _copyToCustomLevels, value))
                _model.CopyToCustomLevels = value;
        }
    }

    public bool CanCopyToCustomLevels => _model.MatchedCustomLevels.Count > 0;
    public int CustomLevelsFolderCount => _model.MatchedCustomLevels.Count;
    public bool HasMultipleCustomLevels => _model.MatchedCustomLevels.Count >= 2;
    public bool IsSingleCustomLevels => _model.MatchedCustomLevels.Count == 1;
    public ObservableCollection<BeatMapFolder> CustomLevelsFolders { get; }

    private BeatMapFolder? _selectedCustomLevelsFolder;
    public BeatMapFolder? SelectedCustomLevelsFolder
    {
        get => _selectedCustomLevelsFolder;
        set
        {
            if (SetProperty(ref _selectedCustomLevelsFolder, value))
            {
                _model.SelectedCustomLevelsFolder = value;
                UpdateOverwriteWarnings();
            }
        }
    }

    // CustomWIPLevels
    private bool _copyToCustomWIPLevels;
    public bool CopyToCustomWIPLevels
    {
        get => _copyToCustomWIPLevels;
        set
        {
            if (SetProperty(ref _copyToCustomWIPLevels, value))
                _model.CopyToCustomWIPLevels = value;
        }
    }

    public bool CanCopyToCustomWIPLevels => _model.MatchedCustomWIPLevels.Count > 0;
    public int CustomWIPLevelsFolderCount => _model.MatchedCustomWIPLevels.Count;
    public bool HasMultipleCustomWIPLevels => _model.MatchedCustomWIPLevels.Count >= 2;
    public bool IsSingleCustomWIPLevels => _model.MatchedCustomWIPLevels.Count == 1;
    public ObservableCollection<BeatMapFolder> CustomWIPLevelsFolders { get; }

    private BeatMapFolder? _selectedCustomWIPLevelsFolder;
    public BeatMapFolder? SelectedCustomWIPLevelsFolder
    {
        get => _selectedCustomWIPLevelsFolder;
        set
        {
            if (SetProperty(ref _selectedCustomWIPLevelsFolder, value))
            {
                _model.SelectedCustomWIPLevelsFolder = value;
                UpdateOverwriteWarnings();
            }
        }
    }

    // Rename
    private RenameOption _renameChoice;
    public RenameOption RenameChoice
    {
        get => _renameChoice;
        set
        {
            if (SetProperty(ref _renameChoice, value))
            {
                _model.RenameChoice = value;
                // RenameChoice変更時は手動編集をクリアして自動生成に戻す
                _model.CustomFileName = null;
                OnPropertyChanged(nameof(RenameDisplayName));
                UpdateOverwriteWarnings();
            }
        }
    }

    public string RenameDisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(_model.CustomFileName))
                return _model.CustomFileName;

            if (_renameChoice == RenameOption.カスタム)
            {
                var settings = _settingsService.Load();
                var tags = new Dictionary<string, string>
                {
                    { "MapId", _model.HexId },
                    { "SongName", _model.SongName },
                    { "SongSubName", _model.Metadata?.SongSubName ?? "" },
                    { "SongAuthorName", _model.Metadata?.SongAuthorName ?? "" },
                    { "LevelAuthorName", _model.Metadata?.LevelAuthorName ?? "" },
                    { "CameraScriptAuthorName", CameraScriptAuthorName },
                    { "FileName", Path.GetFileName(_model.SourceFileName) },
                    { "Bpm", _model.Metadata?.Bpm.ToString() ?? "" }
                };
                string name = NamingEngine.ReplaceTags(settings.CopierRenameCustomFormat, tags);
                if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    name += ".json";
                return name;
            }

            return _renameChoice switch
            {
                RenameOption.無し => System.IO.Path.GetFileName(_model.SourceFileName),
                RenameOption.SongScript => "SongScript.json",
                RenameOption.AuthorIdSongName => $"{CameraScriptAuthorName}_{HexId}_{SongName}_SongScript.json",
                _ => "SongScript.json"
            };
        }
        set
        {
            _model.CustomFileName = value;
            OnPropertyChanged(nameof(RenameDisplayName));
            UpdateOverwriteWarnings();
        }
    }

    // Overwrite warnings
    private bool _hasOverwriteWarningCL;
    public bool HasOverwriteWarningCL
    {
        get => _hasOverwriteWarningCL;
        private set => SetProperty(ref _hasOverwriteWarningCL, value);
    }

    private bool _hasOverwriteWarningWIP;
    public bool HasOverwriteWarningWIP
    {
        get => _hasOverwriteWarningWIP;
        private set => SetProperty(ref _hasOverwriteWarningWIP, value);
    }

    public bool HasOverwriteWarning => _hasOverwriteWarningCL || _hasOverwriteWarningWIP;

    private string _overwriteDetails = "";
    public string OverwriteDetails
    {
        get => _overwriteDetails;
        private set => SetProperty(ref _overwriteDetails, value);
    }

    public void UpdateOverwriteWarnings()
    {
        HasOverwriteWarningCL = SongScriptCopyService.CheckOverwrite(_model, _model.SelectedCustomLevelsFolder);
        HasOverwriteWarningWIP = SongScriptCopyService.CheckOverwrite(_model, _model.SelectedCustomWIPLevelsFolder);
        _model.HasOverwriteWarningCustomLevels = _hasOverwriteWarningCL;
        _model.HasOverwriteWarningCustomWIPLevels = _hasOverwriteWarningWIP;

        var details = new List<string>();
        if (_hasOverwriteWarningCL && _model.SelectedCustomLevelsFolder != null)
        {
            string fileName = SongScriptCopyService.GetTargetFileName(_model);
            details.Add($"CL: {Path.Combine(_model.SelectedCustomLevelsFolder.FolderName, fileName)}");
        }
        if (_hasOverwriteWarningWIP && _model.SelectedCustomWIPLevelsFolder != null)
        {
            string fileName = SongScriptCopyService.GetTargetFileName(_model);
            details.Add($"WIP: {Path.Combine(_model.SelectedCustomWIPLevelsFolder.FolderName, fileName)}");
        }
        OverwriteDetails = details.Count > 0 ? string.Join("\n", details) : "";

        OnPropertyChanged(nameof(HasOverwriteWarning));
    }

    public void UpdateMatchedFolders(
        Dictionary<string, List<BeatMapFolder>> customLevels,
        Dictionary<string, List<BeatMapFolder>> customWIPLevels)
    {
        string key = _model.HexId.ToLowerInvariant();

        _model.MatchedCustomLevels = customLevels.TryGetValue(key, out var clList) ? clList : new();
        _model.MatchedCustomWIPLevels = customWIPLevels.TryGetValue(key, out var wipList) ? wipList : new();

        CustomLevelsFolders.Clear();
        foreach (var f in _model.MatchedCustomLevels) CustomLevelsFolders.Add(f);
        CustomWIPLevelsFolders.Clear();
        foreach (var f in _model.MatchedCustomWIPLevels) CustomWIPLevelsFolders.Add(f);

        if (_model.MatchedCustomLevels.Count > 0)
        {
            CopyToCustomLevels = true;
            SelectedCustomLevelsFolder = _model.MatchedCustomLevels[0];
        }
        else
        {
            CopyToCustomLevels = false;
            SelectedCustomLevelsFolder = null;
        }

        if (_model.MatchedCustomWIPLevels.Count > 0)
        {
            CopyToCustomWIPLevels = true;
            SelectedCustomWIPLevelsFolder = _model.MatchedCustomWIPLevels[0];
        }
        else
        {
            CopyToCustomWIPLevels = false;
            SelectedCustomWIPLevelsFolder = null;
        }

        OnPropertyChanged(nameof(CanCopyToCustomLevels));
        OnPropertyChanged(nameof(CustomLevelsFolderCount));
        OnPropertyChanged(nameof(HasMultipleCustomLevels));
        OnPropertyChanged(nameof(IsSingleCustomLevels));
        OnPropertyChanged(nameof(CanCopyToCustomWIPLevels));
        OnPropertyChanged(nameof(CustomWIPLevelsFolderCount));
        OnPropertyChanged(nameof(HasMultipleCustomWIPLevels));
        OnPropertyChanged(nameof(IsSingleCustomWIPLevels));

        UpdateOverwriteWarnings();
    }
}
