using System.Collections.ObjectModel;
using System.IO;
using CameraScriptManager.Models;
using CameraScriptManager.Services;

namespace CameraScriptManager.ViewModels;

public class SongScriptEntryViewModel : ViewModelBase
{
    private readonly SongScriptEntry _model;
    private readonly SettingsService _settingsService = new();
    private bool _canDownloadMissingBeatmap;
    private bool _isMetadataModified;

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
        _songName = model.SongName;
        _cameraScriptAuthorName = model.CameraScriptAuthorName;
        _hash = model.Hash;
        _songSubName = model.SongSubName;
        _songAuthorName = model.SongAuthorName;
        _levelAuthorName = model.LevelAuthorName;
        _bpm = model.Bpm;
        _duration = model.Duration;
        _avatarHeight = model.AvatarHeight;
        _description = model.Description;
        _renameChoice = model.RenameChoice;
        _hexId = model.HexId;

        // メタデータからのロック状態を初期化
        _isHexIdLocked = model.IsHexIdFromMetadata;
        _isSongNameLocked = model.IsSongNameFromMetadata;
        _isCameraScriptAuthorLocked = model.IsCameraScriptAuthorFromMetadata;
        _isSongSubNameLocked = model.IsSongSubNameFromMetadata;
        _isSongAuthorNameLocked = model.IsSongAuthorNameFromMetadata;
        _isLevelAuthorNameLocked = model.IsLevelAuthorNameFromMetadata;
        _isBpmLocked = model.IsBpmFromMetadata;
        _isAvatarHeightLocked = model.IsAvatarHeightFromMetadata;
        _isDescriptionLocked = model.IsDescriptionFromMetadata;

        if (model.HasOriginalMetadata)
        {
            LockAll();
        }

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

    public bool IsMetadataModified
    {
        get => _isMetadataModified;
        private set => SetProperty(ref _isMetadataModified, value);
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
                OnPropertyChanged(nameof(CanOpenBeatSaver));
                OnPropertyChanged(nameof(RenameDisplayName));
                CanDownloadMissingBeatmap = false;
                MarkMetadataModified();
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
                MarkMetadataModified();
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
        if (IsSongNameLocked)
        {
            return;
        }

        _model.UpdateSongName();
        SongName = _model.SongName;
    }

    public string BeatSaverUrl => _model.BeatSaverUrl;
    public bool CanOpenBeatSaver => !string.IsNullOrWhiteSpace(HexId);
    public string? SourceZipName => _model.SourceZipName;

    public bool CanDownloadMissingBeatmap
    {
        get => _canDownloadMissingBeatmap;
        private set => SetProperty(ref _canDownloadMissingBeatmap, value);
    }

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
                MarkMetadataModified();
            }
        }
    }

    private string _songSubName = "";
    public string SongSubName
    {
        get => _songSubName;
        set
        {
            if (SetProperty(ref _songSubName, value))
            {
                _model.SongSubName = value;
                OnPropertyChanged(nameof(RenameDisplayName));
                UpdateOverwriteWarnings();
                MarkMetadataModified();
            }
        }
    }

    private string _hash = "";
    public string Hash
    {
        get => _hash;
        set
        {
            if (SetProperty(ref _hash, value))
            {
                _model.Hash = value;
                MarkMetadataModified();
            }
        }
    }

    private string _songAuthorName = "";
    public string SongAuthorName
    {
        get => _songAuthorName;
        set
        {
            if (SetProperty(ref _songAuthorName, value))
            {
                _model.SongAuthorName = value;
                OnPropertyChanged(nameof(RenameDisplayName));
                UpdateOverwriteWarnings();
                MarkMetadataModified();
            }
        }
    }

    private string _levelAuthorName = "";
    public string LevelAuthorName
    {
        get => _levelAuthorName;
        set
        {
            if (SetProperty(ref _levelAuthorName, value))
            {
                _model.LevelAuthorName = value;
                OnPropertyChanged(nameof(RenameDisplayName));
                UpdateOverwriteWarnings();
                MarkMetadataModified();
            }
        }
    }

    private double _bpm;
    public double Bpm
    {
        get => _bpm;
        set
        {
            if (SetProperty(ref _bpm, value))
            {
                _model.Bpm = value;
                OnPropertyChanged(nameof(RenameDisplayName));
                UpdateOverwriteWarnings();
                MarkMetadataModified();
            }
        }
    }

    private double _duration;
    public double Duration
    {
        get => _duration;
        set
        {
            if (SetProperty(ref _duration, value))
            {
                _model.Duration = value;
                MarkMetadataModified();
            }
        }
    }

    private double? _avatarHeight;
    public double? AvatarHeight
    {
        get => _avatarHeight;
        set
        {
            if (SetProperty(ref _avatarHeight, value))
            {
                _model.AvatarHeight = value;
                MarkMetadataModified();
            }
        }
    }

    private string _description = "";
    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                _model.Description = value;
                MarkMetadataModified();
            }
        }
    }

    // ロック状態
    private bool _isHexIdLocked;
    public bool IsHexIdLocked
    {
        get => _isHexIdLocked;
        set
        {
            if (SetProperty(ref _isHexIdLocked, value))
                _model.IsHexIdFromMetadata = value;
        }
    }

    private bool _isSongNameLocked;
    public bool IsSongNameLocked
    {
        get => _isSongNameLocked;
        set
        {
            if (SetProperty(ref _isSongNameLocked, value))
                _model.IsSongNameFromMetadata = value;
        }
    }

    private bool _isCameraScriptAuthorLocked;
    public bool IsCameraScriptAuthorLocked
    {
        get => _isCameraScriptAuthorLocked;
        set
        {
            if (SetProperty(ref _isCameraScriptAuthorLocked, value))
                _model.IsCameraScriptAuthorFromMetadata = value;
        }
    }

    private bool _isSongSubNameLocked;
    public bool IsSongSubNameLocked
    {
        get => _isSongSubNameLocked;
        set
        {
            if (SetProperty(ref _isSongSubNameLocked, value))
                _model.IsSongSubNameFromMetadata = value;
        }
    }

    private bool _isSongAuthorNameLocked;
    public bool IsSongAuthorNameLocked
    {
        get => _isSongAuthorNameLocked;
        set
        {
            if (SetProperty(ref _isSongAuthorNameLocked, value))
                _model.IsSongAuthorNameFromMetadata = value;
        }
    }

    private bool _isLevelAuthorNameLocked;
    public bool IsLevelAuthorNameLocked
    {
        get => _isLevelAuthorNameLocked;
        set
        {
            if (SetProperty(ref _isLevelAuthorNameLocked, value))
                _model.IsLevelAuthorNameFromMetadata = value;
        }
    }

    private bool _isBpmLocked;
    public bool IsBpmLocked
    {
        get => _isBpmLocked;
        set
        {
            if (SetProperty(ref _isBpmLocked, value))
                _model.IsBpmFromMetadata = value;
        }
    }

    private bool _isHashLocked;
    public bool IsHashLocked
    {
        get => _isHashLocked;
        set => SetProperty(ref _isHashLocked, value);
    }

    private bool _isDurationLocked;
    public bool IsDurationLocked
    {
        get => _isDurationLocked;
        set => SetProperty(ref _isDurationLocked, value);
    }

    private bool _isAvatarHeightLocked;
    public bool IsAvatarHeightLocked
    {
        get => _isAvatarHeightLocked;
        set
        {
            if (SetProperty(ref _isAvatarHeightLocked, value))
                _model.IsAvatarHeightFromMetadata = value;
        }
    }

    private bool _isDescriptionLocked;
    public bool IsDescriptionLocked
    {
        get => _isDescriptionLocked;
        set
        {
            if (SetProperty(ref _isDescriptionLocked, value))
                _model.IsDescriptionFromMetadata = value;
        }
    }

    public void LockAll()
    {
        IsHexIdLocked = true;
        IsHashLocked = true;
        IsSongNameLocked = true;
        IsCameraScriptAuthorLocked = true;
        IsSongSubNameLocked = true;
        IsSongAuthorNameLocked = true;
        IsLevelAuthorNameLocked = true;
        IsBpmLocked = true;
        IsDurationLocked = true;
        IsAvatarHeightLocked = true;
        IsDescriptionLocked = true;
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

    private void MarkMetadataModified()
    {
        IsMetadataModified = true;
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
            var settings = _settingsService.Load();
            return _model.GenerateRenameDisplayName(settings.CopierRenameCustomFormat ?? "", CameraScriptAuthorName);
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
        _model.UpdateMatchedFolders(customLevels, customWIPLevels);

        CustomLevelsFolders.Clear();
        foreach (var f in _model.MatchedCustomLevels) CustomLevelsFolders.Add(f);
        CustomWIPLevelsFolders.Clear();
        foreach (var f in _model.MatchedCustomWIPLevels) CustomWIPLevelsFolders.Add(f);

        _copyToCustomLevels = _model.CopyToCustomLevels;
        _selectedCustomLevelsFolder = _model.SelectedCustomLevelsFolder;
        _copyToCustomWIPLevels = _model.CopyToCustomWIPLevels;
        _selectedCustomWIPLevelsFolder = _model.SelectedCustomWIPLevelsFolder;

        OnPropertyChanged(nameof(CopyToCustomLevels));
        OnPropertyChanged(nameof(SelectedCustomLevelsFolder));
        OnPropertyChanged(nameof(CopyToCustomWIPLevels));
        OnPropertyChanged(nameof(SelectedCustomWIPLevelsFolder));

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

    public void UpdateBeatmapDownloadAvailability(bool canDownload)
    {
        CanDownloadMissingBeatmap = canDownload;
    }

    public bool ApplyBeatSaverData(BeatSaverApiResponse apiResponse)
    {
        if (apiResponse.Metadata == null)
        {
            _model.Metadata = null;
            return false;
        }

        _model.Metadata = apiResponse.Metadata;
        var metadata = apiResponse.Metadata;
        bool updated = false;

        updated |= ApplyStringValue(() => IsSongNameLocked, SongName, metadata.SongName, value => SongName = value);
        updated |= ApplyStringValue(() => IsSongSubNameLocked, SongSubName, metadata.SongSubName, value => SongSubName = value);
        updated |= ApplyStringValue(() => IsSongAuthorNameLocked, SongAuthorName, metadata.SongAuthorName, value => SongAuthorName = value);
        updated |= ApplyStringValue(() => IsLevelAuthorNameLocked, LevelAuthorName, metadata.LevelAuthorName, value => LevelAuthorName = value);
        updated |= ApplyDoubleValue(() => IsBpmLocked, Bpm, metadata.Bpm, value => Bpm = value);
        updated |= ApplyDoubleValue(() => IsDurationLocked, Duration, metadata.Duration, value => Duration = value);

        return updated;
    }

    private static bool ApplyStringValue(Func<bool> isLocked, string? currentValue, string? newValue, Action<string> apply)
    {
        if (isLocked() ||
            string.IsNullOrWhiteSpace(newValue) ||
            string.Equals(currentValue ?? "", newValue, StringComparison.Ordinal))
        {
            return false;
        }

        apply(newValue);
        return true;
    }

    private static bool ApplyDoubleValue(Func<bool> isLocked, double currentValue, double newValue, Action<double> apply)
    {
        if (isLocked() || newValue <= 0 || currentValue.Equals(newValue))
        {
            return false;
        }

        apply(newValue);
        return true;
    }
}
