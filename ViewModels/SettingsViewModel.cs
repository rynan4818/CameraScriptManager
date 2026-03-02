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

        LoadSettings();
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

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        _customLevelsPath = settings.CustomLevelsPath;
        _customWIPLevelsPath = settings.CustomWIPLevelsPath;
        _originalScriptPath1 = settings.OriginalScriptPath1;
        _originalScriptPath2 = settings.OriginalScriptPath2;
        _originalScriptPath3 = settings.OriginalScriptPath3;
        OnPropertyChanged(nameof(OriginalScriptPath3));
    }

    private void SaveSettings()
    {
        _settingsService.Save(new AppSettings
        {
            CustomLevelsPath = CustomLevelsPath,
            CustomWIPLevelsPath = CustomWIPLevelsPath,
            OriginalScriptPath1 = OriginalScriptPath1,
            OriginalScriptPath2 = OriginalScriptPath2,
            OriginalScriptPath3 = OriginalScriptPath3
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
