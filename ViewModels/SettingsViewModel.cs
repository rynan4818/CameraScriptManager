using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CameraScriptManager.Models;
using CameraScriptManager.Services;
using Microsoft.Win32;

namespace CameraScriptManager.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService = new();

    public SettingsViewModel()
    {
        BrowseCustomLevelsCommand = new RelayCommand(BrowseCustomLevels);
        BrowseCustomWIPLevelsCommand = new RelayCommand(BrowseCustomWIPLevels);
        BrowseOriginalScript1Command = new RelayCommand(BrowseOriginalScript1);
        BrowseOriginalScript2Command = new RelayCommand(BrowseOriginalScript2);
        BrowseOriginalScript3Command = new RelayCommand(BrowseOriginalScript3);
        BrowseSongScriptsFolderCommand = new RelayCommand(BrowseSongScriptsFolder);
        BrowseBackupRootCommand = new RelayCommand(BrowseBackupRoot);
        CopyTagCommand = new RelayCommand(CopyTag);
        ResetColumnWidthsCommand = new RelayCommand(ResetColumnWidths);

        LoadSettings();
    }

    private void CopyTag(object? parameter)
    {
        if (parameter is string tag && !string.IsNullOrEmpty(tag))
        {
            System.Windows.Clipboard.SetText($"{{{tag}}}");
        }
    }

    private string _customLevelsPath = "";
    public string CustomLevelsPath
    {
        get => _customLevelsPath;
        set
        {
            if (SetProperty(ref _customLevelsPath, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private string _customWIPLevelsPath = "";
    public string CustomWIPLevelsPath
    {
        get => _customWIPLevelsPath;
        set
        {
            if (SetProperty(ref _customWIPLevelsPath, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private string _originalScriptPath1 = "";
    public string OriginalScriptPath1
    {
        get => _originalScriptPath1;
        set
        {
            if (SetProperty(ref _originalScriptPath1, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private string _originalScriptPath2 = "";
    public string OriginalScriptPath2
    {
        get => _originalScriptPath2;
        set
        {
            if (SetProperty(ref _originalScriptPath2, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private string _originalScriptPath3 = "";
    public string OriginalScriptPath3
    {
        get => _originalScriptPath3;
        set
        {
            if (SetProperty(ref _originalScriptPath3, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private string _songScriptsFolderPath = "";
    public string SongScriptsFolderPath
    {
        get => _songScriptsFolderPath;
        set
        {
            if (SetProperty(ref _songScriptsFolderPath, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private string _backupRootPath = "";
    public string BackupRootPath
    {
        get => _backupRootPath;
        set
        {
            if (SetProperty(ref _backupRootPath, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    // Manager Zip Naming
    private bool _isManagerZipNamingDefault = true;
    public bool IsManagerZipNamingDefault
    {
        get => _isManagerZipNamingDefault;
        set
        {
            if (SetProperty(ref _isManagerZipNamingDefault, value))
            {
                OnPropertyChanged(nameof(IsManagerZipNamingCustom));
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    public bool IsManagerZipNamingCustom
    {
        get => !IsManagerZipNamingDefault;
        set => IsManagerZipNamingDefault = !value;
    }

    private string _managerZipCustomFormat = "";
    public string ManagerZipCustomFormat
    {
        get => _managerZipCustomFormat;
        set
        {
            if (SetProperty(ref _managerZipCustomFormat, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private string _managerZipPackagingMode = ZipExportService.PackagingFolderKeepOriginalJson;

    public bool IsManagerZipPackagingFolderKeepOriginalJson
    {
        get => _managerZipPackagingMode == ZipExportService.PackagingFolderKeepOriginalJson;
        set
        {
            if (value)
            {
                SetManagerZipPackagingMode(ZipExportService.PackagingFolderKeepOriginalJson);
            }
        }
    }

    public bool IsManagerZipPackagingFlatRenameJson
    {
        get => _managerZipPackagingMode == ZipExportService.PackagingFlatRenameJson;
        set
        {
            if (value)
            {
                SetManagerZipPackagingMode(ZipExportService.PackagingFlatRenameJson);
            }
        }
    }

    public bool IsManagerZipPackagingFolderSongScriptJson
    {
        get => _managerZipPackagingMode == ZipExportService.PackagingFolderSongScriptJson;
        set
        {
            if (value)
            {
                SetManagerZipPackagingMode(ZipExportService.PackagingFolderSongScriptJson);
            }
        }
    }



    private string _copierRenameCustomFormat = "";
    public string CopierRenameCustomFormat
    {
        get => _copierRenameCustomFormat;
        set
        {
            if (SetProperty(ref _copierRenameCustomFormat, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private bool _enableMapScriptsBackup = true;
    public bool EnableMapScriptsBackup
    {
        get => _enableMapScriptsBackup;
        set
        {
            if (SetProperty(ref _enableMapScriptsBackup, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private bool _enableSongScriptsBackup = true;
    public bool EnableSongScriptsBackup
    {
        get => _enableSongScriptsBackup;
        set
        {
            if (SetProperty(ref _enableSongScriptsBackup, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private bool _enableCopierBackup = true;
    public bool EnableCopierBackup
    {
        get => _enableCopierBackup;
        set
        {
            if (SetProperty(ref _enableCopierBackup, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    private bool _enableAutoUpdateCheck = true;
    public bool EnableAutoUpdateCheck
    {
        get => _enableAutoUpdateCheck;
        set
        {
            if (SetProperty(ref _enableAutoUpdateCheck, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

    public string CurrentVersion => AppUpdateCheckService.GetCurrentVersionString();
    public string ReleaseUrl => AppUpdateCheckService.ReleasePageUrl;

    public event EventHandler? SettingsChanged;

    private void OnSettingsChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public ICommand BrowseCustomLevelsCommand { get; }
    public ICommand BrowseCustomWIPLevelsCommand { get; }
    public ICommand BrowseOriginalScript1Command { get; }
    public ICommand BrowseOriginalScript2Command { get; }
    public ICommand BrowseOriginalScript3Command { get; }
    public ICommand BrowseSongScriptsFolderCommand { get; }
    public ICommand BrowseBackupRootCommand { get; }
    public ICommand CopyTagCommand { get; }
    public ICommand ResetColumnWidthsCommand { get; }

    private void SetManagerZipPackagingMode(string mode)
    {
        if (_managerZipPackagingMode == mode)
        {
            return;
        }

        _managerZipPackagingMode = mode;
        OnPropertyChanged(nameof(IsManagerZipPackagingFolderKeepOriginalJson));
        OnPropertyChanged(nameof(IsManagerZipPackagingFlatRenameJson));
        OnPropertyChanged(nameof(IsManagerZipPackagingFolderSongScriptJson));
        SaveSettings();
        OnSettingsChanged();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        _customLevelsPath = settings.CustomLevelsPath;
        _customWIPLevelsPath = settings.CustomWIPLevelsPath;
        _originalScriptPath1 = settings.OriginalScriptPath1;
        _originalScriptPath2 = settings.OriginalScriptPath2;
        _originalScriptPath3 = settings.OriginalScriptPath3;
        _songScriptsFolderPath = settings.SongScriptsFolderPath;
        _backupRootPath = settings.BackupRootPath;

        _isManagerZipNamingDefault = settings.ManagerZipNamingMode != "Custom";
        _managerZipCustomFormat = string.IsNullOrEmpty(settings.ManagerZipCustomFormat)
            ? "{MapId}_{SongName}_{LevelAuthorName}"
            : settings.ManagerZipCustomFormat;
        _managerZipPackagingMode = string.IsNullOrWhiteSpace(settings.ManagerZipPackagingMode)
            ? ZipExportService.PackagingFolderKeepOriginalJson
            : settings.ManagerZipPackagingMode;


        _copierRenameCustomFormat = string.IsNullOrEmpty(settings.CopierRenameCustomFormat)
            ? "{MapId}_{CameraScriptAuthorName}_{SongName}_SongScript"
            : settings.CopierRenameCustomFormat;

        _enableMapScriptsBackup = settings.EnableMapScriptsBackup;
        _enableSongScriptsBackup = settings.EnableSongScriptsBackup;
        _enableCopierBackup = settings.EnableCopierBackup;
        _enableAutoUpdateCheck = settings.EnableAutoUpdateCheck;

        OnPropertyChanged(nameof(CustomLevelsPath));
        OnPropertyChanged(nameof(CustomWIPLevelsPath));
        OnPropertyChanged(nameof(OriginalScriptPath1));
        OnPropertyChanged(nameof(OriginalScriptPath2));
        OnPropertyChanged(nameof(OriginalScriptPath3));
        OnPropertyChanged(nameof(SongScriptsFolderPath));
        OnPropertyChanged(nameof(BackupRootPath));
        OnPropertyChanged(nameof(IsManagerZipNamingDefault));
        OnPropertyChanged(nameof(IsManagerZipNamingCustom));
        OnPropertyChanged(nameof(ManagerZipCustomFormat));
        OnPropertyChanged(nameof(IsManagerZipPackagingFolderKeepOriginalJson));
        OnPropertyChanged(nameof(IsManagerZipPackagingFlatRenameJson));
        OnPropertyChanged(nameof(IsManagerZipPackagingFolderSongScriptJson));

        OnPropertyChanged(nameof(CopierRenameCustomFormat));
        OnPropertyChanged(nameof(EnableMapScriptsBackup));
        OnPropertyChanged(nameof(EnableSongScriptsBackup));
        OnPropertyChanged(nameof(EnableCopierBackup));
        OnPropertyChanged(nameof(EnableAutoUpdateCheck));
        OnPropertyChanged(nameof(CurrentVersion));
        OnPropertyChanged(nameof(ReleaseUrl));
    }

    private void SaveSettings()
    {
        var currentSettings = _settingsService.Load();
        currentSettings.CustomLevelsPath = CustomLevelsPath;
        currentSettings.CustomWIPLevelsPath = CustomWIPLevelsPath;
        currentSettings.OriginalScriptPath1 = OriginalScriptPath1;
        currentSettings.OriginalScriptPath2 = OriginalScriptPath2;
        currentSettings.OriginalScriptPath3 = OriginalScriptPath3;
        currentSettings.SongScriptsFolderPath = SongScriptsFolderPath;
        currentSettings.BackupRootPath = BackupRootPath;
        currentSettings.ManagerZipNamingMode = IsManagerZipNamingDefault ? "Default" : "Custom";
        currentSettings.ManagerZipCustomFormat = ManagerZipCustomFormat;
        currentSettings.ManagerZipPackagingMode = _managerZipPackagingMode;
        currentSettings.CopierRenameNamingMode = "Custom";
        currentSettings.CopierRenameCustomFormat = CopierRenameCustomFormat;
        currentSettings.EnableMapScriptsBackup = EnableMapScriptsBackup;
        currentSettings.EnableSongScriptsBackup = EnableSongScriptsBackup;
        currentSettings.EnableCopierBackup = EnableCopierBackup;
        currentSettings.EnableAutoUpdateCheck = EnableAutoUpdateCheck;
        _settingsService.Save(currentSettings);
    }

    private void BrowseCustomLevels()
    {
        var path = BrowseFolder("CustomLevelsフォルダを選択", CustomLevelsPath);
        if (path != null)
            CustomLevelsPath = path;
    }

    private void BrowseCustomWIPLevels()
    {
        var path = BrowseFolder("CustomWIPLevelsフォルダを選択", CustomWIPLevelsPath);
        if (path != null)
            CustomWIPLevelsPath = path;
    }

    private void BrowseOriginalScript1()
    {
        var path = BrowseFolder("元データ検索フォルダ1を選択", OriginalScriptPath1);
        if (path != null)
            OriginalScriptPath1 = path;
    }

    private void BrowseOriginalScript2()
    {
        var path = BrowseFolder("元データ検索フォルダ2を選択", OriginalScriptPath2);
        if (path != null)
            OriginalScriptPath2 = path;
    }

    private void BrowseOriginalScript3()
    {
        var path = BrowseFolder("元データ検索フォルダ3を選択", OriginalScriptPath3);
        if (path != null)
            OriginalScriptPath3 = path;
    }

    private void BrowseSongScriptsFolder()
    {
        var path = BrowseFolder("SongScriptsフォルダを選択", SongScriptsFolderPath);
        if (path != null)
            SongScriptsFolderPath = path;
    }

    private void BrowseBackupRoot()
    {
        var path = BrowseFolder("バックアップルートフォルダを選択", BackupRootPath);
        if (path != null)
            BackupRootPath = path;
    }

    private void ResetColumnWidths()
    {
        ColumnWidthSettingsService.ResetAllColumnWidths();
        OnSettingsChanged();
    }

    private static string? BrowseFolder(string description, string currentPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = description
        };

        if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
            dialog.InitialDirectory = currentPath;

        if (dialog.ShowDialog() == true)
            return dialog.FolderName;

        return null;
    }
}
