using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CameraScriptManager.Services;
using Microsoft.Win32;

namespace CameraScriptManager.ViewModels;

public class ManagerViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService = new();
    private readonly CameraScriptScanner _scanner = new();
    private readonly SongDetailsCacheService _cacheService = new();
    private readonly BeatSaverApiClient _apiClient;
    private readonly IDialogService _dialogService = new DialogService();
    private string _customLevelsPath = "";
    private string _customWIPLevelsPath = "";
    private string _originalScriptPath1 = "";
    private string _originalScriptPath2 = "";
    private string _originalScriptPath3 = "";
    private string _backupRootPath = "";
    private bool _enableMapScriptsBackup;
    private string _statusText = "";

    public string CustomLevelsPath => _customLevelsPath;
    public string CustomWIPLevelsPath => _customWIPLevelsPath;

    public ManagerViewModel()
    {
        _apiClient = new BeatSaverApiClient(_cacheService);
        Items = new ObservableCollection<CameraScriptItemViewModel>();

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        AddMetadataCommand = new AsyncRelayCommand(AddMetadataAsync);
        ExportFolderCommand = new AsyncRelayCommand(ExportFolderAsync);
        ExportZipCommand = new AsyncRelayCommand(ExportZipAsync);
        FindOriginalScriptsCommand = new AsyncRelayCommand(FindOriginalScriptsAsync);
        CreatePlaylistCommand = new AsyncRelayCommand(CreatePlaylistAsync);

        LoadSettings();

        // SongDetailsCacheをバックグラウンドで初期化
        _ = _cacheService.InitAsync();
    }

    public ObservableCollection<CameraScriptItemViewModel> Items { get; }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand AddMetadataCommand { get; }
    public AsyncRelayCommand ExportFolderCommand { get; }
    public AsyncRelayCommand ExportZipCommand { get; }
    public AsyncRelayCommand FindOriginalScriptsCommand { get; }
    public AsyncRelayCommand CreatePlaylistCommand { get; }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        _customLevelsPath = settings.CustomLevelsPath;
        _customWIPLevelsPath = settings.CustomWIPLevelsPath;
        _originalScriptPath1 = settings.OriginalScriptPath1;
        _originalScriptPath2 = settings.OriginalScriptPath2;
        _originalScriptPath3 = settings.OriginalScriptPath3;
        _backupRootPath = BackupPathResolver.ResolveBackupRootPath(settings);
        _enableMapScriptsBackup = settings.EnableMapScriptsBackup;
    }

    public void ReloadSettings()
    {
        LoadSettings();
    }

    private bool EnsureAnyBeatmapPathConfigured(bool showMessage)
    {
        if (!string.IsNullOrWhiteSpace(_customLevelsPath) || !string.IsNullOrWhiteSpace(_customWIPLevelsPath))
        {
            return true;
        }

        StatusText = "CustomLevelsまたはCustomWIPLevelsパスが設定されていません";
        if (showMessage)
        {
            _dialogService.ShowMessageBox(
                "SettingsでCustomLevelsまたはCustomWIPLevelsパスを設定して下さい。",
                "情報",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return false;
    }

    private bool EnsureSourcePathConfigured(string sourceType, bool showMessage)
    {
        string displayName = string.Equals(sourceType, "CustomWIPLevels", StringComparison.OrdinalIgnoreCase)
            ? "CustomWIPLevels"
            : "CustomLevels";
        string path = displayName == "CustomWIPLevels" ? _customWIPLevelsPath : _customLevelsPath;

        if (!string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        StatusText = $"{displayName}パスが設定されていません";
        if (showMessage)
        {
            _dialogService.ShowMessageBox(
                $"Settingsで{displayName}パスを設定して下さい。",
                "情報",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return false;
    }

    public bool EnsureSelectedSourcePathsConfigured(IEnumerable<CameraScriptItemViewModel> items)
    {
        bool hasCustomLevelsItem = items.Any(item =>
            string.Equals(item.SourceType, "CustomLevels", StringComparison.OrdinalIgnoreCase));
        if (hasCustomLevelsItem && !EnsureSourcePathConfigured("CustomLevels", showMessage: true))
        {
            return false;
        }

        bool hasCustomWipLevelsItem = items.Any(item =>
            string.Equals(item.SourceType, "CustomWIPLevels", StringComparison.OrdinalIgnoreCase));
        if (hasCustomWipLevelsItem && !EnsureSourcePathConfigured("CustomWIPLevels", showMessage: true))
        {
            return false;
        }

        return true;
    }

    public bool TryBuildMapFolderPath(CameraScriptItemViewModel item, out string folderPath)
    {
        folderPath = string.Empty;
        string sourceType = string.Equals(item.SourceType, "CustomWIPLevels", StringComparison.OrdinalIgnoreCase)
            ? "CustomWIPLevels"
            : "CustomLevels";

        if (!EnsureSourcePathConfigured(sourceType, showMessage: true))
        {
            return false;
        }

        string rootPath = sourceType == "CustomWIPLevels" ? _customWIPLevelsPath : _customLevelsPath;
        folderPath = Path.Combine(rootPath, item.FolderName);
        return true;
    }



    private Task ScanAsync()
    {
        if (!EnsureAnyBeatmapPathConfigured(showMessage: true))
        {
            return Task.CompletedTask;
        }

        StatusText = "スキャン中...";

        var entries = new System.Collections.Generic.List<CameraScriptManager.Models.CameraScriptEntry>();

        _dialogService.ShowProgressDialog("カメラスクリプトを読込中...", async (_) =>
        {
            entries = await Task.Run(() => _scanner.Scan(_customLevelsPath, _customWIPLevelsPath));
        });

        Items.Clear();

        foreach (var entry in entries)
        {
            Items.Add(new CameraScriptItemViewModel(entry));
        }

        StatusText = $"スキャン完了: {Items.Count} 件のカメラスクリプトが見つかりました";
        return Task.CompletedTask;
    }

    public async Task FetchBeatSaverMetadataAsync(CameraScriptItemViewModel item)
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
            item.ApplyBeatSaverData(response);
            StatusText = $"BeatSaverメタデータ取得完了: {item.MapId}";
        }
        else
        {
            StatusText = $"BeatSaverメタデータが見つかりませんでした: {item.MapId}";
        }
    }

    private Task AddMetadataAsync()
    {
        var targetItems = Items.Where(i => i.IsSelected).ToList();
        if (targetItems.Count == 0)
        {
            StatusText = "選択された項目がありません";
            _dialogService.ShowMessageBox("処理を行う項目を選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        if (!EnsureSelectedSourcePathsConfigured(targetItems))
        {
            return Task.CompletedTask;
        }

        string backupMessage = _enableMapScriptsBackup
            ? $"変更前のファイルは {BackupPathResolver.GetMapScriptsBackupDirectory(_backupRootPath)} にバックアップされます。"
            : "バックアップは作成されません。";

        var result = _dialogService.ShowMessageBoxWithResult(
            $"選択された {targetItems.Count} 件のカメラスクリプトにメタデータを追加します。\n{backupMessage}\nよろしいですか？",
            "メタ情報追加",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return Task.CompletedTask;

        StatusText = "メタ情報追加中...";

        var files = new List<(string fullFilePath, string originalJson, string newJson, string sourceType)>();

        foreach (var item in targetItems)
        {
            string originalJson = item.GetOriginalJsonContent();
            var newJson = MetadataService.PrepareJsonWithMetadata(
                originalJson,
                item.MapId,
                item.CameraScriptAuthorName,
                item.Bpm,
                item.Duration,
                item.SongName,
                item.SongSubName,
                item.SongAuthorName,
                item.LevelAuthorName,
                item.AvatarHeight,
                item.Description);

            files.Add((item.FullFilePath, originalJson, newJson, item.SourceType));
        }

        try
        {
            MetadataService.CreateBackupAndWriteMetadata(
                files,
                _customLevelsPath,
                _customWIPLevelsPath,
                _backupRootPath,
                _enableMapScriptsBackup);

            // Update internal state after writing
            foreach (var item in targetItems)
            {
                item.Entry.JsonContent = File.ReadAllText(item.FullFilePath);
                item.Entry.HasOriginalMetadata = true;
                item.LockAll();
                item.IsModified = false;
            }

            StatusText = $"メタ情報追加完了: {targetItems.Count} 件のファイルを更新しました";
        }
        catch (Exception ex)
        {
            StatusText = $"エラー: {ex.Message}";
            _dialogService.ShowMessageBox($"メタ情報の追加中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    private Task ExportZipAsync()
    {
        var selectedItems = Items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            StatusText = "エクスポートする項目を選択してください";
            _dialogService.ShowMessageBox("エクスポートする項目を選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        bool isSingle = selectedItems.Count == 1;
        string defaultFileName = isSingle 
            ? ZipExportService.SanitizeFileName($"{selectedItems[0].MapId}_{selectedItems[0].SongName}_{selectedItems[0].LevelAuthorName}.zip")
            : "CameraScripts.zip";

        var fileName = _dialogService.ShowSaveFileDialog(defaultFileName, "ZIPファイル (*.zip)|*.zip", "カメラスクリプトのエクスポート");
        if (fileName == null) return Task.CompletedTask;

        StatusText = "ZIPエクスポート中...";

        var items = BuildExportItems(selectedItems);

        try
        {
            ZipExportService.Export(items, fileName);
            StatusText = $"ZIPエクスポート完了: {fileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"エラー: {ex.Message}";
            _dialogService.ShowMessageBox($"ZIPエクスポート中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    private Task ExportFolderAsync()
    {
        var selectedItems = Items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            StatusText = "エクスポートする項目を選択してください";
            _dialogService.ShowMessageBox("エクスポートする項目を選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        string? rootFolderPath = _dialogService.ShowOpenFolderDialog("出力先フォルダを選択", _customLevelsPath);
        if (string.IsNullOrWhiteSpace(rootFolderPath))
        {
            return Task.CompletedTask;
        }

        StatusText = "フォルダ出力中...";

        var items = BuildExportItems(selectedItems);

        try
        {
            string outputDirectoryPath = ZipExportService.CreateTimestampedOutputDirectory(rootFolderPath);
            ZipExportService.ExportToDirectory(items, outputDirectoryPath);
            StatusText = $"フォルダ出力完了: {outputDirectoryPath}";

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", outputDirectoryPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessageBox(
                    $"フォルダ出力は完了しましたが、保存先フォルダを開けませんでした:\n{ex.Message}",
                    "情報",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"エラー: {ex.Message}";
            _dialogService.ShowMessageBox($"フォルダ出力中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    private List<(string zipEntryFolder, string fileName, string jsonContent)> BuildExportItems(
        IList<CameraScriptItemViewModel> selectedItems)
    {
        var items = new List<(string zipEntryFolder, string fileName, string jsonContent)>();
        var settings = _settingsService.Load();

        foreach (var selected in selectedItems)
        {
            var (zipEntryFolder, zipEntryFileName) = ZipExportService.GetZipEntryParts(
                settings.ManagerZipPackagingMode ?? ZipExportService.PackagingFolderKeepOriginalJson,
                settings.ManagerZipNamingMode ?? "",
                settings.ManagerZipCustomFormat ?? "",
                selected.Entry,
                selected.CameraScriptAuthorName ?? "",
                selected.FileName);

            string content = selected.GetCurrentJsonContent();
            items.Add((zipEntryFolder, zipEntryFileName, content));
        }

        return items;
    }

    private Task FindOriginalScriptsAsync()
    {
        var targetItems = Items.ToList();
        if (targetItems.Count == 0)
        {
            StatusText = "カメラスクリプトが見つかりません";
            return Task.CompletedTask;
        }

        var searchPaths = new List<string>
        {
            _originalScriptPath1,
            _originalScriptPath2,
            _originalScriptPath3
        };

        _dialogService.ShowProgressDialog("元データを検索中...", async (progress) =>
        {
            var service = new CameraScriptManager.Services.OriginalScriptMatchService(searchPaths, (msg, pct) =>
            {
                progress(msg, pct);
                Application.Current.Dispatcher.Invoke(() => StatusText = msg);
            });
            var entries = targetItems.Select(i => i.Entry).ToList();
            await service.MatchOriginalScriptsAsync(entries);
        });

        // Update view models with matching results
        int matchCount = 0;
        foreach (var item in targetItems)
        {
            item.OriginalSourceFiles.Clear();
            foreach (var match in item.Entry.OriginalSourceFiles)
            {
                item.OriginalSourceFiles.Add(match);
            }

            item.SelectedOriginalSourceFile = item.OriginalSourceFiles.FirstOrDefault();

            if (item.Entry.OriginalSourceFiles.Count > 0)
            {
                matchCount++;
            }
        }

        StatusText = $"元データ照合完了: {matchCount}/{targetItems.Count} 件のファイルがマッチしました";
        return Task.CompletedTask;
    }

    private Task CreatePlaylistAsync()
    {
        var selectedItems = Items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            StatusText = "プレイリストを作成する項目を選択してください";
            _dialogService.ShowMessageBox("プレイリストを作成する項目を選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        var vm = new CreatePlaylistViewModel();
        if (_dialogService.ShowCreatePlaylistDialog(vm))
        {
            var defaultFileName = ZipExportService.SanitizeFileName(vm.Title) + ".bplist";
            var savePath = _dialogService.ShowSaveFileDialog(defaultFileName, "BeatSaber Playlist (*.bplist)|*.bplist", "プレイリストの保存");
            
            if (!string.IsNullOrWhiteSpace(savePath))
            {
                StatusText = "プレイリスト作成中...";
                try
                {
                    var entries = selectedItems.Select(i => i.Entry).ToList();
                    PlaylistExportService.ExportToBplist(savePath, vm.Title, vm.Author, vm.Description, vm.CoverImagePath, entries);
                    StatusText = $"プレイリスト作成完了: {Path.GetFileName(savePath)}";
                    _dialogService.ShowMessageBox("プレイリストの作成が完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusText = $"プレイリスト作成エラー: {ex.Message}";
                    _dialogService.ShowMessageBox($"プレイリストの作成中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        return Task.CompletedTask;
    }

}
