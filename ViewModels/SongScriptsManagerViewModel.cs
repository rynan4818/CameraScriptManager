using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CameraScriptManager.Models;
using CameraScriptManager.Services;

namespace CameraScriptManager.ViewModels;

public class SongScriptsManagerViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService = new();
    private readonly SongScriptsScanner _scanner = new();
    private readonly SongScriptsSaveService _saveService = new();
    private readonly SongDetailsCacheService _cacheService = new();
    private readonly BeatSaverApiClient _apiClient;
    private readonly IDialogService _dialogService = new DialogService();

    private string _songScriptsFolderPath = "";
    private string _songScriptsBackupFolderPath = "";
    private string _statusText = "";
    private string _songScriptsFolderDisplayPath = "";
    private string _backupFolderDisplayPath = "";

    public SongScriptsManagerViewModel()
    {
        _apiClient = new BeatSaverApiClient(_cacheService);
        Items = new ObservableCollection<SongScriptsManagerItemViewModel>();
        ScanCommand = new AsyncRelayCommand(ScanAsync);
        SaveCheckedCommand = new AsyncRelayCommand(SaveCheckedAsync);

        LoadSettings();
        _ = _cacheService.InitAsync();
        _ = ScanCoreAsync(showProgressDialog: false);
    }

    public ObservableCollection<SongScriptsManagerItemViewModel> Items { get; }

    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand SaveCheckedCommand { get; }

    public string SongScriptsFolderDisplayPath
    {
        get => _songScriptsFolderDisplayPath;
        private set => SetProperty(ref _songScriptsFolderDisplayPath, value);
    }

    public string BackupFolderDisplayPath
    {
        get => _backupFolderDisplayPath;
        private set => SetProperty(ref _backupFolderDisplayPath, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public void ReloadSettings()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        _songScriptsFolderPath = SongScriptsPathResolver.ResolveSongScriptsFolderPath(settings);
        _songScriptsBackupFolderPath = SongScriptsPathResolver.ResolveSongScriptsBackupFolderPath(settings);

        SongScriptsFolderDisplayPath = _songScriptsFolderPath;
        BackupFolderDisplayPath = string.IsNullOrWhiteSpace(_songScriptsBackupFolderPath)
            ? "(元データと同じフォルダ)"
            : _songScriptsBackupFolderPath;
    }

    private Task ScanAsync()
    {
        return ScanCoreAsync(showProgressDialog: true);
    }

    private async Task ScanCoreAsync(bool showProgressDialog)
    {
        LoadSettings();

        if (string.IsNullOrWhiteSpace(_songScriptsFolderPath))
        {
            StatusText = "SongScriptsフォルダが設定されていません";
            Items.Clear();
            return;
        }

        if (!Directory.Exists(_songScriptsFolderPath))
        {
            StatusText = $"SongScriptsフォルダが見つかりません: {_songScriptsFolderPath}";
            Items.Clear();
            return;
        }

        StatusText = "SongScriptsをスキャン中...";
        var scannedEntries = new List<SongScriptsManagerEntry>();

        if (showProgressDialog)
        {
            _dialogService.ShowProgressDialog("SongScriptsを読込中...", async progress =>
            {
                scannedEntries = await Task.Run(() =>
                    _scanner.Scan(_songScriptsFolderPath, (message, percent) => progress(message, percent)));
            });
        }
        else
        {
            scannedEntries = await Task.Run(() => _scanner.Scan(_songScriptsFolderPath));
        }

        Items.Clear();
        foreach (var entry in scannedEntries)
        {
            Items.Add(new SongScriptsManagerItemViewModel(entry));
        }

        StatusText = $"SongScripts読込完了: {Items.Count} 件";
    }

    private async Task SaveCheckedAsync()
    {
        var targetItems = Items.Where(item => item.IsSaveChecked).ToList();
        if (targetItems.Count == 0)
        {
            StatusText = "保存対象がありません";
            _dialogService.ShowMessageBox("保存する項目にチェックを入れてください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = _dialogService.ShowMessageBoxWithResult(
            $"チェックされた {targetItems.Count} 件のmetadataを上書き保存します。\n既存データは .bak でバックアップします。\nよろしいですか？",
            "SongScripts保存",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        StatusText = "SongScriptsを保存中...";

        List<SongScriptsSaveResult> saveResults = new();
        _dialogService.ShowProgressDialog("SongScriptsを保存中...", async progress =>
        {
            var saveProgress = new Progress<string>(message => progress(message, null));
            saveResults = await _saveService.SaveAsync(
                targetItems.Select(item => item.Model).ToList(),
                _songScriptsFolderPath,
                _songScriptsBackupFolderPath,
                saveProgress);
        });

        var successfulEntries = saveResults
            .Where(saveResult => saveResult.Success)
            .SelectMany(saveResult => saveResult.Entries)
            .ToHashSet();

        foreach (var item in targetItems.Where(item => successfulEntries.Contains(item.Model)))
        {
            item.ApplySavedState();
        }

        int successCount = saveResults.Where(saveResult => saveResult.Success).Sum(saveResult => saveResult.Entries.Count);
        int failCount = saveResults.Count(saveResult => !saveResult.Success);

        if (failCount == 0)
        {
            StatusText = $"保存完了: {successCount} 件";
            return;
        }

        StatusText = $"保存完了: {successCount} 件成功, {failCount} 件失敗";
        string errorLines = string.Join(
            Environment.NewLine,
            saveResults
                .Where(saveResult => !saveResult.Success)
                .Select(saveResult => $"{Path.GetFileName(saveResult.SourceFilePath)}: {saveResult.ErrorMessage}"));
        _dialogService.ShowMessageBox(
            $"一部保存に失敗しました。\n{errorLines}",
            "保存エラー",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    public async Task FetchBeatSaverMetadataAsync(SongScriptsManagerItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(item.MapId))
        {
            StatusText = "IDが空のため、BeatSaverから取得できません";
            return;
        }

        StatusText = $"BeatSaver API取得中: {item.MapId}...";

        var (response, _, _) = await _apiClient.GetMapAsync(item.MapId);
        if (response?.Metadata != null)
        {
            bool updated = item.ApplyBeatSaverData(response);
            StatusText = updated
                ? $"BeatSaverメタデータ取得完了: {item.MapId}"
                : $"BeatSaverメタデータ取得完了(更新なし): {item.MapId}";
        }
        else
        {
            StatusText = $"BeatSaverメタデータが見つかりませんでした: {item.MapId}";
        }
    }
}
