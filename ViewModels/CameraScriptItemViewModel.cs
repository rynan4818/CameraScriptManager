using CameraScriptManager.Models;

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
    private bool _isCameraScriptAuthorLocked;
    private bool _suppressModifiedTracking;
    private string _originalSourceFile = "";

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
        _isCameraScriptAuthorLocked = entry.IsCameraScriptAuthorFromMetadata;
        _originalSourceFile = entry.OriginalSourceFile;

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
                MarkModified();
        }
    }

    public double Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    public bool IsCameraScriptAuthorLocked
    {
        get => _isCameraScriptAuthorLocked;
        set => SetProperty(ref _isCameraScriptAuthorLocked, value);
    }

    public string OriginalSourceFile
    {
        get => _originalSourceFile;
        set
        {
            if (SetProperty(ref _originalSourceFile, value))
            {
                _entry.OriginalSourceFile = value;
            }
        }
    }

    public string FileName => _entry.FileName;
    public string FolderName => _entry.FolderName;
    public string SourceType => _entry.SourceType;
    public string FullFilePath => _entry.FullFilePath;
    public string FolderPath => _entry.FolderPath;

    public string GetCurrentJsonContent()
    {
        if (IsModified)
        {
            return Services.MetadataService.PrepareJsonWithMetadata(
                _entry.JsonContent,
                MapId,
                CameraScriptAuthorName,
                Bpm,
                Duration,
                SongName,
                SongSubName,
                SongAuthorName,
                LevelAuthorName);
        }
        return _entry.JsonContent;
    }

    public void ApplyBeatSaverData(Models.BeatSaverApiResponse apiResponse)
    {
        _suppressModifiedTracking = true;

        if (apiResponse.Metadata != null)
        {
            var meta = apiResponse.Metadata;

            if (!string.IsNullOrWhiteSpace(apiResponse.Id))
                MapId = apiResponse.Id;

            if (meta.Bpm > 0)
                Bpm = meta.Bpm;

            if (meta.Duration > 0)
                Duration = meta.Duration;

            if (!string.IsNullOrWhiteSpace(meta.SongName))
                SongName = meta.SongName;

            if (!string.IsNullOrWhiteSpace(meta.SongSubName))
                SongSubName = meta.SongSubName;

            if (!string.IsNullOrWhiteSpace(meta.SongAuthorName))
                SongAuthorName = meta.SongAuthorName;

            if (!string.IsNullOrWhiteSpace(meta.LevelAuthorName))
                LevelAuthorName = meta.LevelAuthorName;
        }

        _suppressModifiedTracking = false;
        IsModified = true;
    }

    private void MarkModified()
    {
        if (!_suppressModifiedTracking)
            IsModified = true;
    }
}
