using CameraScriptManager.Models;

namespace CameraScriptManager.ViewModels;

public class SongScriptsManagerItemViewModel : ViewModelBase
{
    private readonly SongScriptsManagerEntry _model;
    private bool _suppressModifiedTracking;
    private bool _isSaveChecked;
    private bool _isModified;
    private string _mapId = "";
    private string _hash = "";
    private string _cameraScriptAuthorName = "";
    private string _songName = "";
    private string _songSubName = "";
    private string _songAuthorName = "";
    private string _levelAuthorName = "";
    private double _bpm;
    private double _duration;
    private double _avatarHeight;
    private string _description = "";
    private bool _isMapIdLocked;
    private bool _isHashLocked;
    private bool _isCameraScriptAuthorLocked;
    private bool _isSongNameLocked;
    private bool _isSongSubNameLocked;
    private bool _isSongAuthorNameLocked;
    private bool _isLevelAuthorNameLocked;
    private bool _isBpmLocked;
    private bool _isDurationLocked;
    private bool _isAvatarHeightLocked;
    private bool _isDescriptionLocked;

    public SongScriptsManagerItemViewModel(SongScriptsManagerEntry model)
    {
        _model = model;
        _suppressModifiedTracking = true;

        _mapId = model.MapId;
        _hash = model.Hash;
        _cameraScriptAuthorName = model.CameraScriptAuthorName;
        _songName = model.SongName;
        _songSubName = model.SongSubName;
        _songAuthorName = model.SongAuthorName;
        _levelAuthorName = model.LevelAuthorName;
        _bpm = model.Bpm;
        _duration = model.Duration;
        _avatarHeight = model.AvatarHeight;
        _description = model.Description;

        if (model.HasMetadataBlock)
        {
            LockAll();
        }

        _suppressModifiedTracking = false;
    }

    public SongScriptsManagerEntry Model => _model;
    public string SourceDisplayPath => _model.SourceDisplayPath;
    public string SourceFilePath => _model.SourceFilePath;
    public string SourceType => _model.IsZipEntry ? "ZIP" : "JSON";

    public bool IsSaveChecked
    {
        get => _isSaveChecked;
        set => SetProperty(ref _isSaveChecked, value);
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
            {
                _model.MapId = value;
                MarkModified();
            }
        }
    }

    public string Hash
    {
        get => _hash;
        set
        {
            if (SetProperty(ref _hash, value))
            {
                _model.Hash = value;
                MarkModified();
            }
        }
    }

    public string CameraScriptAuthorName
    {
        get => _cameraScriptAuthorName;
        set
        {
            if (SetProperty(ref _cameraScriptAuthorName, value))
            {
                _model.CameraScriptAuthorName = value;
                MarkModified();
            }
        }
    }

    public string SongName
    {
        get => _songName;
        set
        {
            if (SetProperty(ref _songName, value))
            {
                _model.SongName = value;
                MarkModified();
            }
        }
    }

    public string SongSubName
    {
        get => _songSubName;
        set
        {
            if (SetProperty(ref _songSubName, value))
            {
                _model.SongSubName = value;
                MarkModified();
            }
        }
    }

    public string SongAuthorName
    {
        get => _songAuthorName;
        set
        {
            if (SetProperty(ref _songAuthorName, value))
            {
                _model.SongAuthorName = value;
                MarkModified();
            }
        }
    }

    public string LevelAuthorName
    {
        get => _levelAuthorName;
        set
        {
            if (SetProperty(ref _levelAuthorName, value))
            {
                _model.LevelAuthorName = value;
                MarkModified();
            }
        }
    }

    public double Bpm
    {
        get => _bpm;
        set
        {
            if (SetProperty(ref _bpm, value))
            {
                _model.Bpm = value;
                MarkModified();
            }
        }
    }

    public double Duration
    {
        get => _duration;
        set
        {
            if (SetProperty(ref _duration, value))
            {
                _model.Duration = value;
                MarkModified();
            }
        }
    }

    public double AvatarHeight
    {
        get => _avatarHeight;
        set
        {
            if (SetProperty(ref _avatarHeight, value))
            {
                _model.AvatarHeight = value;
                MarkModified();
            }
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                _model.Description = value;
                MarkModified();
            }
        }
    }

    public bool IsMapIdLocked
    {
        get => _isMapIdLocked;
        set => SetProperty(ref _isMapIdLocked, value);
    }

    public bool IsHashLocked
    {
        get => _isHashLocked;
        set => SetProperty(ref _isHashLocked, value);
    }

    public bool IsCameraScriptAuthorLocked
    {
        get => _isCameraScriptAuthorLocked;
        set => SetProperty(ref _isCameraScriptAuthorLocked, value);
    }

    public bool IsSongNameLocked
    {
        get => _isSongNameLocked;
        set => SetProperty(ref _isSongNameLocked, value);
    }

    public bool IsSongSubNameLocked
    {
        get => _isSongSubNameLocked;
        set => SetProperty(ref _isSongSubNameLocked, value);
    }

    public bool IsSongAuthorNameLocked
    {
        get => _isSongAuthorNameLocked;
        set => SetProperty(ref _isSongAuthorNameLocked, value);
    }

    public bool IsLevelAuthorNameLocked
    {
        get => _isLevelAuthorNameLocked;
        set => SetProperty(ref _isLevelAuthorNameLocked, value);
    }

    public bool IsBpmLocked
    {
        get => _isBpmLocked;
        set => SetProperty(ref _isBpmLocked, value);
    }

    public bool IsDurationLocked
    {
        get => _isDurationLocked;
        set => SetProperty(ref _isDurationLocked, value);
    }

    public bool IsAvatarHeightLocked
    {
        get => _isAvatarHeightLocked;
        set => SetProperty(ref _isAvatarHeightLocked, value);
    }

    public bool IsDescriptionLocked
    {
        get => _isDescriptionLocked;
        set => SetProperty(ref _isDescriptionLocked, value);
    }

    public void LockAll()
    {
        IsMapIdLocked = true;
        IsHashLocked = true;
        IsCameraScriptAuthorLocked = true;
        IsSongNameLocked = true;
        IsSongSubNameLocked = true;
        IsSongAuthorNameLocked = true;
        IsLevelAuthorNameLocked = true;
        IsBpmLocked = true;
        IsDurationLocked = true;
        IsAvatarHeightLocked = true;
        IsDescriptionLocked = true;
    }

    public void ApplySavedState()
    {
        _suppressModifiedTracking = true;
        LockAll();
        IsModified = false;
        IsSaveChecked = false;
        _suppressModifiedTracking = false;
    }

    public bool ApplyBeatSaverData(BeatSaverApiResponse apiResponse)
    {
        if (apiResponse.Metadata == null)
            return false;

        bool updated = false;
        var metadata = apiResponse.Metadata;

        _suppressModifiedTracking = true;
        try
        {
            updated |= ApplyStringValue(() => IsMapIdLocked, MapId, apiResponse.Id, value => MapId = value);
            updated |= ApplyDoubleValue(() => IsBpmLocked, Bpm, metadata.Bpm, value => Bpm = value);
            updated |= ApplyDoubleValue(() => IsDurationLocked, Duration, metadata.Duration, value => Duration = value);
            updated |= ApplyStringValue(() => IsSongNameLocked, SongName, metadata.SongName, value => SongName = value);
            updated |= ApplyStringValue(() => IsSongSubNameLocked, SongSubName, metadata.SongSubName, value => SongSubName = value);
            updated |= ApplyStringValue(() => IsSongAuthorNameLocked, SongAuthorName, metadata.SongAuthorName, value => SongAuthorName = value);
            updated |= ApplyStringValue(() => IsLevelAuthorNameLocked, LevelAuthorName, metadata.LevelAuthorName, value => LevelAuthorName = value);
        }
        finally
        {
            _suppressModifiedTracking = false;
        }

        if (updated)
        {
            IsModified = true;
            IsSaveChecked = true;
        }

        return updated;
    }

    private void MarkModified()
    {
        if (_suppressModifiedTracking)
            return;

        IsModified = true;
        IsSaveChecked = true;
    }

    private bool ApplyStringValue(Func<bool> isLocked, string? currentValue, string? newValue, Action<string> apply)
    {
        if (isLocked() ||
            string.IsNullOrWhiteSpace(newValue) ||
            string.Equals(currentValue ?? "", newValue, StringComparison.Ordinal))
            return false;

        apply(newValue);
        return true;
    }

    private bool ApplyDoubleValue(Func<bool> isLocked, double currentValue, double newValue, Action<double> apply)
    {
        if (isLocked() || newValue <= 0 || currentValue.Equals(newValue))
            return false;

        apply(newValue);
        return true;
    }
}
