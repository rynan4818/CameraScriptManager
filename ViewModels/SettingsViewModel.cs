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
        CopyTagCommand = new RelayCommand(CopyTag);

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

    private bool _createBackup = true;
    public bool CreateBackup
    {
        get => _createBackup;
        set
        {
            if (SetProperty(ref _createBackup, value))
            {
                SaveSettings();
                OnSettingsChanged();
            }
        }
    }

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
    public ICommand CopyTagCommand { get; }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        _customLevelsPath = settings.CustomLevelsPath;
        _customWIPLevelsPath = settings.CustomWIPLevelsPath;
        _originalScriptPath1 = settings.OriginalScriptPath1;
        _originalScriptPath2 = settings.OriginalScriptPath2;
        _originalScriptPath3 = settings.OriginalScriptPath3;

        _isManagerZipNamingDefault = settings.ManagerZipNamingMode != "Custom";
        _managerZipCustomFormat = string.IsNullOrEmpty(settings.ManagerZipCustomFormat)
            ? "{MapId}_{SongName}_{LevelAuthorName}"
            : settings.ManagerZipCustomFormat;


        _copierRenameCustomFormat = string.IsNullOrEmpty(settings.CopierRenameCustomFormat)
            ? "{CameraScriptAuthorName}_{MapId}_{SongName}_SongScript"
            : settings.CopierRenameCustomFormat;

        _createBackup = settings.CreateBackup;

        OnPropertyChanged(nameof(CustomLevelsPath));
        OnPropertyChanged(nameof(CustomWIPLevelsPath));
        OnPropertyChanged(nameof(OriginalScriptPath1));
        OnPropertyChanged(nameof(OriginalScriptPath2));
        OnPropertyChanged(nameof(OriginalScriptPath3));
        OnPropertyChanged(nameof(IsManagerZipNamingDefault));
        OnPropertyChanged(nameof(IsManagerZipNamingCustom));
        OnPropertyChanged(nameof(ManagerZipCustomFormat));

        OnPropertyChanged(nameof(CopierRenameCustomFormat));
        OnPropertyChanged(nameof(CreateBackup));
    }

    private void SaveSettings()
    {
        _settingsService.Save(new AppSettings
        {
            CustomLevelsPath = CustomLevelsPath,
            CustomWIPLevelsPath = CustomWIPLevelsPath,
            OriginalScriptPath1 = OriginalScriptPath1,
            OriginalScriptPath2 = OriginalScriptPath2,
            OriginalScriptPath3 = OriginalScriptPath3,
            ManagerZipNamingMode = IsManagerZipNamingDefault ? "Default" : "Custom",
            ManagerZipCustomFormat = ManagerZipCustomFormat,
            CopierRenameNamingMode = "Custom",
            CopierRenameCustomFormat = CopierRenameCustomFormat,
            CreateBackup = CreateBackup
        });
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
