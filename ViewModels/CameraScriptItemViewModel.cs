using CameraScriptManager.Models;
using CameraScriptManager.Services;
using System.IO;

namespace CameraScriptManager.ViewModels;

public class CameraScriptItemViewModel : ViewModelBase
{
    private readonly CameraScriptEntry _entry;
    private bool _isSelected;
    private bool _isModified;
    private string _mapId = "";
    private string _cameraScriptAuthorName = "";
    private string _songName = "";
    private string _songSubName = "";
    private string _songAuthorName = "";
    private string _levelAuthorName = "";
    private double _bpm;
    private double _duration;
    private double? _avatarHeight;
    private string _description = "";
    private bool _isCameraScriptAuthorLocked;
    private bool _isMapIdLocked;
    private bool _isSongNameLocked;
    private bool _isSongSubNameLocked;
    private bool _isSongAuthorNameLocked;
    private bool _isLevelAuthorNameLocked;
    private bool _isBpmLocked;
    private bool _isAvatarHeightLocked;
    private bool _isDescriptionLocked;
    private bool _suppressModifiedTracking;
    private string? _selectedOriginalSourceFile;

    public CameraScriptItemViewModel(CameraScriptEntry entry)
    {
        _entry = entry;
        _suppressModifiedTracking = true;

        _mapId = entry.MapId;
        _cameraScriptAuthorName = entry.CameraScriptAuthorName;
        _songName = entry.SongName;
        _songSubName = entry.SongSubName;
        _songAuthorName = entry.SongAuthorName;
        _levelAuthorName = entry.LevelAuthorName;
        _bpm = entry.Bpm;
        _duration = entry.Duration;
        _avatarHeight = entry.AvatarHeight;
        _description = entry.Description;
        _isCameraScriptAuthorLocked = entry.IsCameraScriptAuthorFromMetadata;
        _isMapIdLocked = entry.IsMapIdFromMetadata;
        _isSongNameLocked = entry.IsSongNameFromMetadata;
        _isSongSubNameLocked = entry.IsSongSubNameFromMetadata;
        _isSongAuthorNameLocked = entry.IsSongAuthorNameFromMetadata;
        _isLevelAuthorNameLocked = entry.IsLevelAuthorNameFromMetadata;
        _isBpmLocked = entry.IsBpmFromMetadata;
        _isAvatarHeightLocked = entry.IsAvatarHeightFromMetadata;
        _isDescriptionLocked = entry.IsDescriptionFromMetadata;
        
        OriginalSourceFiles = new System.Collections.ObjectModel.ObservableCollection<string>(entry.OriginalSourceFiles);
        _selectedOriginalSourceFile = OriginalSourceFiles.FirstOrDefault();

        if (entry.HasOriginalMetadata)
        {
            LockAll();
        }

        // If no original metadata but Info.dat provided data, mark as modified
        if (!entry.HasOriginalMetadata &&
            (!string.IsNullOrWhiteSpace(entry.SongName) ||
             !string.IsNullOrWhiteSpace(entry.SongAuthorName) ||
             !string.IsNullOrWhiteSpace(entry.LevelAuthorName) ||
             entry.Bpm > 0))
        {
            _isModified = true;
        }

        _suppressModifiedTracking = false;
    }

    public CameraScriptEntry Entry => _entry;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsModified
    {
        get => _isModified;
        set => SetProperty(ref _isModified, value);
    }

    public string MapId
    {
        get => _mapId;
        set
        {
            if (SetProperty(ref _mapId, value))
                MarkModified();
        }
    }

    public string CameraScriptAuthorName
    {
        get => _cameraScriptAuthorName;
        set
        {
            if (SetProperty(ref _cameraScriptAuthorName, value))
                MarkModified();
        }
    }

    public string SongName
    {
        get => _songName;
        set
        {
            if (SetProperty(ref _songName, value))
                MarkModified();
        }
    }

    public string SongSubName
    {
        get => _songSubName;
        set
        {
            if (SetProperty(ref _songSubName, value))
                MarkModified();
        }
    }

    public string SongAuthorName
    {
        get => _songAuthorName;
        set
        {
            if (SetProperty(ref _songAuthorName, value))
                MarkModified();
        }
    }

    public string LevelAuthorName
    {
        get => _levelAuthorName;
        set
        {
            if (SetProperty(ref _levelAuthorName, value))
                MarkModified();
        }
    }

    public double Bpm
    {
        get => _bpm;
        set
        {
            if (SetProperty(ref _bpm, value))
            {
                MarkModified();
                OnPropertyChanged(nameof(DurationDiffBeat));
                OnPropertyChanged(nameof(DurationDiffBeatText));
            }
        }
    }

    public double Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    public double? AvatarHeight
    {
        get => _avatarHeight;
        set
        {
            if (SetProperty(ref _avatarHeight, value))
                MarkModified();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
                MarkModified();
        }
    }

    public double ScriptDuration => _entry.ScriptDuration;
    public double OggDuration => _entry.OggDuration;

    public string ScriptDurationText => FormatDuration(ScriptDuration);
    public string OggDurationText => FormatDuration(OggDuration);

    public double DurationDiffBeat => (ScriptDuration / 60.0 * Bpm) - (OggDuration / 60.0 * Bpm);
    public string DurationDiffBeatText => DurationDiffBeat.ToString("F2");

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "0:00";
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }

    public string Hash => _entry.Hash;

    public bool IsCameraScriptAuthorLocked
    {
        get => _isCameraScriptAuthorLocked;
        set
        {
            if (SetProperty(ref _isCameraScriptAuthorLocked, value))
                _entry.IsCameraScriptAuthorFromMetadata = value;
        }
    }

    public bool IsMapIdLocked
    {
        get => _isMapIdLocked;
        set
        {
            if (SetProperty(ref _isMapIdLocked, value))
                _entry.IsMapIdFromMetadata = value;
        }
    }

    public bool IsSongNameLocked
    {
        get => _isSongNameLocked;
        set
        {
            if (SetProperty(ref _isSongNameLocked, value))
                _entry.IsSongNameFromMetadata = value;
        }
    }

    public bool IsSongSubNameLocked
    {
        get => _isSongSubNameLocked;
        set
        {
            if (SetProperty(ref _isSongSubNameLocked, value))
                _entry.IsSongSubNameFromMetadata = value;
        }
    }

    public bool IsSongAuthorNameLocked
    {
        get => _isSongAuthorNameLocked;
        set
        {
            if (SetProperty(ref _isSongAuthorNameLocked, value))
                _entry.IsSongAuthorNameFromMetadata = value;
        }
    }

    public bool IsLevelAuthorNameLocked
    {
        get => _isLevelAuthorNameLocked;
        set
        {
            if (SetProperty(ref _isLevelAuthorNameLocked, value))
                _entry.IsLevelAuthorNameFromMetadata = value;
        }
    }

    public bool IsBpmLocked
    {
        get => _isBpmLocked;
        set
        {
            if (SetProperty(ref _isBpmLocked, value))
                _entry.IsBpmFromMetadata = value;
        }
    }

    public bool IsAvatarHeightLocked
    {
        get => _isAvatarHeightLocked;
        set
        {
            if (SetProperty(ref _isAvatarHeightLocked, value))
                _entry.IsAvatarHeightFromMetadata = value;
        }
    }

    public bool IsDescriptionLocked
    {
        get => _isDescriptionLocked;
        set
        {
            if (SetProperty(ref _isDescriptionLocked, value))
                _entry.IsDescriptionFromMetadata = value;
        }
    }

    public string? SelectedOriginalSourceFile
    {
        get => _selectedOriginalSourceFile;
        set => SetProperty(ref _selectedOriginalSourceFile, value);
    }

    public System.Collections.ObjectModel.ObservableCollection<string> OriginalSourceFiles { get; }

    public string FileName => _entry.FileName;
    public string FolderName => _entry.FolderName;
    public string SourceType => _entry.SourceType;
    public string FullFilePath => _entry.FullFilePath;
    public string FolderPath => _entry.FolderPath;

    public string GetCurrentJsonContent()
    {
        string originalJson = GetOriginalJsonContent();
        if (IsModified)
        {
            return Services.MetadataService.PrepareJsonWithMetadata(
                originalJson,
                MapId,
                CameraScriptAuthorName,
                Bpm,
                Duration,
                SongName,
                SongSubName,
                SongAuthorName,
                LevelAuthorName,
                AvatarHeight,
                Description);
        }
        return originalJson;
    }

    public string GetOriginalJsonContent()
    {
        if (!string.IsNullOrEmpty(_entry.JsonContent))
        {
            return _entry.JsonContent;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_entry.FullFilePath) && File.Exists(_entry.FullFilePath))
            {
                _entry.JsonContent = File.ReadAllText(_entry.FullFilePath);
            }
        }
        catch
        {
        }

        return _entry.JsonContent;
    }

    public void ApplyBeatSaverData(Models.BeatSaverApiResponse apiResponse)
    {
        if (apiResponse.Metadata == null)
        {
            return;
        }

        bool updated = false;
        _suppressModifiedTracking = true;

        try
        {
            var meta = apiResponse.Metadata;

            updated |= ApplyStringValue(() => IsMapIdLocked, MapId, apiResponse.Id, value => MapId = value);
            updated |= ApplyDoubleValue(() => IsBpmLocked, Bpm, meta.Bpm, value => Bpm = value);
            updated |= ApplyStringValue(() => IsSongNameLocked, SongName, meta.SongName, value => SongName = value);
            updated |= ApplyStringValue(() => IsSongSubNameLocked, SongSubName, meta.SongSubName, value => SongSubName = value);
            updated |= ApplyStringValue(() => IsSongAuthorNameLocked, SongAuthorName, meta.SongAuthorName, value => SongAuthorName = value);
            updated |= ApplyStringValue(() => IsLevelAuthorNameLocked, LevelAuthorName, meta.LevelAuthorName, value => LevelAuthorName = value);

            if (!_entry.HasOriginalMetadata && meta.Duration > 0 && !_duration.Equals(meta.Duration))
            {
                Duration = meta.Duration;
                updated = true;
            }
        }
        finally
        {
            _suppressModifiedTracking = false;
        }

        if (updated)
        {
            IsModified = true;
        }
    }

    private void MarkModified()
    {
        if (!_suppressModifiedTracking)
            IsModified = true;
    }

    public bool ApplyInfoDatData(InfoDatData infoDat)
    {
        bool updated = false;
        _suppressModifiedTracking = true;

        try
        {
            updated |= ApplySupplementStringValue(() => IsSongNameLocked, SongName, infoDat.SongName, value => SongName = value);
            updated |= ApplySupplementStringValue(() => IsSongSubNameLocked, SongSubName, infoDat.SongSubName, value => SongSubName = value);
            updated |= ApplySupplementStringValue(() => IsSongAuthorNameLocked, SongAuthorName, infoDat.SongAuthorName, value => SongAuthorName = value);
            updated |= ApplySupplementStringValue(() => IsLevelAuthorNameLocked, LevelAuthorName, infoDat.LevelAuthorName, value => LevelAuthorName = value);
            updated |= ApplySupplementDoubleValue(() => IsBpmLocked, Bpm, infoDat.Bpm, value => Bpm = value);
        }
        finally
        {
            _suppressModifiedTracking = false;
        }

        if (updated)
        {
            IsModified = true;
        }

        return updated;
    }

    public void LockAll()
    {
        IsCameraScriptAuthorLocked = true;
        IsMapIdLocked = true;
        IsSongNameLocked = true;
        IsSongSubNameLocked = true;
        IsSongAuthorNameLocked = true;
        IsLevelAuthorNameLocked = true;
        IsBpmLocked = true;
        IsAvatarHeightLocked = true;
        IsDescriptionLocked = true;
    }

    private bool ApplyStringValue(Func<bool> isLocked, string? currentValue, string? newValue, Action<string> apply)
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

    private bool ApplyDoubleValue(Func<bool> isLocked, double currentValue, double newValue, Action<double> apply)
    {
        if (isLocked() || newValue <= 0 || currentValue.Equals(newValue))
        {
            return false;
        }

        apply(newValue);
        return true;
    }

    private bool ApplySupplementStringValue(Func<bool> isLocked, string? currentValue, string? newValue, Action<string> apply)
    {
        if (isLocked() ||
            !string.IsNullOrWhiteSpace(currentValue) ||
            string.IsNullOrWhiteSpace(newValue))
        {
            return false;
        }

        apply(newValue);
        return true;
    }

    private bool ApplySupplementDoubleValue(Func<bool> isLocked, double currentValue, double newValue, Action<double> apply)
    {
        if (isLocked() || currentValue > 0 || newValue <= 0)
        {
            return false;
        }

        apply(newValue);
        return true;
    }
}
