using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
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
    private readonly CameraSongScriptCompatibleBeatmapIndexService _beatmapIndexService;
    private readonly SongScriptsMissingBeatmapDownloadService _missingBeatmapDownloadService;
    private readonly IDialogService _dialogService = new DialogService();
    private readonly SemaphoreSlim _scanSemaphore = new(1, 1);

    private string _songScriptsFolderPath = "";
    private string _backupRootPath = "";
    private string _customLevelsPath = "";
    private string _customWipLevelsPath = "";
    private string _statusText = "";
    private string _songScriptsFolderDisplayPath = "";
    private string _backupFolderDisplayPath = "";
    private string _hashScanStatusText = "hash検索: 未実行";
    private bool _isHashScanRunning;
    private double _hashScanProgressValue;
    private bool _showMetadataColumns = false;
    private bool _enableSongScriptsBackup;
    private CameraSongScriptCompatibleBeatmapIndex _beatmapIndex = new();
    private CancellationTokenSource? _hashScanCancellationTokenSource;
    private bool _isBeatmapMatchFinalized = true;
    private int _scanGeneration;

    public SongScriptsManagerViewModel()
    {
        _apiClient = new BeatSaverApiClient(_cacheService);
        _beatmapIndexService = new CameraSongScriptCompatibleBeatmapIndexService(_cacheService);
        _missingBeatmapDownloadService = new SongScriptsMissingBeatmapDownloadService(_apiClient);
        Items = new ObservableCollection<SongScriptsManagerItemViewModel>();
        ScanCommand = new AsyncRelayCommand(ScanAsync);
        SaveCheckedCommand = new AsyncRelayCommand(SaveCheckedAsync);
        CreatePlaylistCommand = new AsyncRelayCommand(CreatePlaylistAsync);
        DownloadMissingBeatmapCommand = new AsyncRelayCommand(DownloadMissingBeatmapAsync);

        LoadSettings();
        UpdateInitialStatus();
        _ = _cacheService.InitAsync();
    }

    public ObservableCollection<SongScriptsManagerItemViewModel> Items { get; }

    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand SaveCheckedCommand { get; }
    public AsyncRelayCommand CreatePlaylistCommand { get; }
    public AsyncRelayCommand DownloadMissingBeatmapCommand { get; }

    public Task DownloadSelectedMissingBeatmapsAsync(IEnumerable<SongScriptsManagerItemViewModel> items)
    {
        return DownloadMissingBeatmapsCoreAsync(items);
    }

    private Task CreatePlaylistAsync()
    {
        var selectedItems = Items.Where(item => item.IsSaveChecked).ToList();
        return CreatePlaylistForItemsAsync(selectedItems);
    }

    private async Task CreatePlaylistForItemsAsync(IEnumerable<SongScriptsManagerItemViewModel> sourceItems)
    {
        var selectedItems = sourceItems.ToList();
        if (selectedItems.Count == 0)
        {
            StatusText = "プレイリストを作成する項目にチェックを入れてください";
            _dialogService.ShowMessageBox("プレイリストを作成する項目にチェックを入れてください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var vm = new CreatePlaylistViewModel();
        if (!_dialogService.ShowCreatePlaylistDialog(vm))
        {
            return;
        }

        string defaultFileName = ZipExportService.SanitizeFileName(vm.Title) + ".bplist";
        string? savePath = _dialogService.ShowSaveFileDialog(defaultFileName, "BeatSaber Playlist (*.bplist)|*.bplist", "プレイリストの保存");
        if (string.IsNullOrWhiteSpace(savePath))
        {
            return;
        }

        StatusText = "プレイリスト作成中...";
        try
        {
            PlaylistExportService.ExportToBplist(
                savePath,
                vm.Title,
                vm.Author,
                vm.Description,
                vm.CoverImagePath,
                selectedItems.Select(item => item.Model));

            StatusText = $"プレイリスト作成完了: {Path.GetFileName(savePath)}";
            _dialogService.ShowMessageBox("プレイリストの作成が完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"プレイリスト作成エラー: {ex.Message}";
            _dialogService.ShowMessageBox($"プレイリストの作成中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

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

    public string HashScanStatusText
    {
        get => _hashScanStatusText;
        private set => SetProperty(ref _hashScanStatusText, value);
    }

    public bool IsHashScanRunning
    {
        get => _isHashScanRunning;
        private set => SetProperty(ref _isHashScanRunning, value);
    }

    public double HashScanProgressValue
    {
        get => _hashScanProgressValue;
        private set => SetProperty(ref _hashScanProgressValue, value);
    }

    public bool ShowMetadataColumns
    {
        get => _showMetadataColumns;
        set
        {
            if (SetProperty(ref _showMetadataColumns, value))
            {
                SaveSettings();
            }
        }
    }

    public void ReloadSettings()
    {
        LoadSettings();
        if (_scanGeneration == 0 && Items.Count == 0 && !IsHashScanRunning)
        {
            UpdateInitialStatus();
        }
    }

    public Task InitializeAsync()
    {
        LoadSettings();
        if (!EnsureSongScriptsPathConfigured(showMessage: false))
        {
            SetSongScriptsPathMissingState();
            return Task.CompletedTask;
        }

        return ScanCoreAsync(showProgressDialog: true);
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        _customLevelsPath = settings.CustomLevelsPath;
        _customWipLevelsPath = settings.CustomWIPLevelsPath;
        _songScriptsFolderPath = SongScriptsPathResolver.ResolveSongScriptsFolderPath(settings);
        _backupRootPath = BackupPathResolver.ResolveBackupRootPath(settings);
        _enableSongScriptsBackup = settings.EnableSongScriptsBackup;
        SetProperty(ref _showMetadataColumns, settings.ShowMetadataColumns);

        SongScriptsFolderDisplayPath = _songScriptsFolderPath;
        BackupFolderDisplayPath = _enableSongScriptsBackup
            ? BackupPathResolver.GetSongScriptsBackupDirectory(_backupRootPath)
            : "無効";
    }

    private void UpdateInitialStatus()
    {
        if (string.IsNullOrWhiteSpace(_songScriptsFolderPath))
        {
            StatusText = "SongScriptsパスが設定されていません";
            return;
        }

        if (!Directory.Exists(_songScriptsFolderPath))
        {
            StatusText = $"SongScriptsフォルダが見つかりません: {_songScriptsFolderPath}";
            return;
        }

        StatusText = "SongScripts をスキャンしてください";
    }

    private void SetSongScriptsPathMissingState()
    {
        _beatmapIndex = new CameraSongScriptCompatibleBeatmapIndex();
        _isBeatmapMatchFinalized = true;
        Items.Clear();
        SetHashScanIdle("hash検索: 未実行");
    }

    private bool EnsureSongScriptsPathConfigured(bool showMessage)
    {
        if (!string.IsNullOrWhiteSpace(_songScriptsFolderPath))
        {
            return true;
        }

        StatusText = "SongScriptsパスが設定されていません";
        if (showMessage)
        {
            _dialogService.ShowMessageBox(
                "SettingsでSongScriptsパスを設定して下さい。",
                "情報",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return false;
    }

    private bool EnsureCustomLevelsPathConfigured(bool showMessage)
    {
        if (!string.IsNullOrWhiteSpace(_customLevelsPath))
        {
            return true;
        }

        StatusText = "CustomLevelsパスが設定されていません";
        if (showMessage)
        {
            _dialogService.ShowMessageBox(
                "SettingsでCustomLevelsパスを設定して下さい。",
                "情報",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return false;
    }

    private void SaveSettings()
    {
        var settings = _settingsService.Load();
        settings.ShowMetadataColumns = ShowMetadataColumns;
        _settingsService.Save(settings);
    }

    private Task ScanAsync()
    {
        LoadSettings();
        if (!EnsureSongScriptsPathConfigured(showMessage: true))
        {
            SetSongScriptsPathMissingState();
            return Task.CompletedTask;
        }

        return ScanCoreAsync(showProgressDialog: true);
    }

    private async Task ScanCoreAsync(bool showProgressDialog)
    {
        await _scanSemaphore.WaitAsync();
        try
        {
            int scanGeneration = BeginBeatmapLookup();
            LoadSettings();

            if (string.IsNullOrWhiteSpace(_songScriptsFolderPath))
            {
                SetSongScriptsPathMissingState();
                StatusText = "SongScriptsパスが設定されていません";
                return;
            }

            if (!Directory.Exists(_songScriptsFolderPath))
            {
                _beatmapIndex = new CameraSongScriptCompatibleBeatmapIndex();
                _isBeatmapMatchFinalized = true;
                Items.Clear();
                SetHashScanIdle("hash検索: 未実行");
                StatusText = $"SongScriptsフォルダが見つかりません: {_songScriptsFolderPath}";
                return;
            }

            StatusText = "SongDetailsCacheを確認中...";
            await _cacheService.EnsureInitializedAsync();

            StatusText = "SongScriptsをスキャン中...";
            var scannedEntries = new List<SongScriptsManagerEntry>();
            CameraSongScriptCompatibleBeatmapIndex? beatmapIndex = null;

            if (showProgressDialog)
            {
                _dialogService.ShowProgressDialog("SongScriptsを読込中...", async progress =>
                {
                    await Task.Run(() =>
                    {
                        scannedEntries = _scanner.Scan(_songScriptsFolderPath, (message, percent) =>
                            progress(message, percent.HasValue ? percent.Value * 0.7 : null));

                        beatmapIndex = _beatmapIndexService.ScanByMapId(
                            _customLevelsPath,
                            _customWipLevelsPath,
                            (message, percent) =>
                            {
                                double? scaledPercent = percent.HasValue
                                    ? 70 + (percent.Value * 0.3)
                                    : null;
                                progress(message, scaledPercent);
                            });
                    });
                });
            }
            else
            {
                scannedEntries = await Task.Run(() => _scanner.Scan(_songScriptsFolderPath));
                beatmapIndex = await Task.Run(() => _beatmapIndexService.ScanByMapId(_customLevelsPath, _customWipLevelsPath));
            }

            if (scanGeneration != _scanGeneration)
            {
                return;
            }

            _beatmapIndex = beatmapIndex ?? new CameraSongScriptCompatibleBeatmapIndex();
            _isBeatmapMatchFinalized = false;

            Items.Clear();
            foreach (var entry in scannedEntries)
            {
                var item = new SongScriptsManagerItemViewModel(entry)
                {
                    OnLevelReferenceChanged = UpdateBeatmapMatchState
                };
                ApplyBeatmapMatchState(item);
                Items.Add(item);
            }

            if (_beatmapIndex.BeatmapFolders.Count == 0)
            {
                CompleteHashScan(scanGeneration, _beatmapIndex, "hash検索: 対象なし", 100);
                return;
            }

            StatusText = $"SongScripts読込完了: {Items.Count} 件 (ID検索結果を表示中)";
            SetHashScanPending(_beatmapIndex.BeatmapFolders.Count);
            StartHashScan(scanGeneration);
        }
        finally
        {
            _scanSemaphore.Release();
        }
    }

    private async Task SaveCheckedAsync()
    {
        var targetItems = Items.Where(item => item.IsSaveChecked).ToList();
        if (targetItems.Count == 0)
        {
            StatusText = "メタ情報追加対象がありません";
            _dialogService.ShowMessageBox("メタ情報を追加する項目にチェックを入れてください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LoadSettings();
        if (!EnsureSongScriptsPathConfigured(showMessage: true))
        {
            return;
        }

        string backupMessage = _enableSongScriptsBackup
            ? $"既存データは {BackupFolderDisplayPath} にバックアップします。"
            : "バックアップは作成されません。";

        var result = _dialogService.ShowMessageBoxWithResult(
            $"チェックされた {targetItems.Count} 件にメタ情報を追加して元ファイルを更新します。\n{backupMessage}\nよろしいですか？",
            "メタ情報追加",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        StatusText = "メタ情報追加中...";

        List<SongScriptsSaveResult> saveResults = new();
        _dialogService.ShowProgressDialog("メタ情報を追加中...", async progress =>
        {
            var saveProgress = new Progress<string>(message => progress(message, null));
            saveResults = await _saveService.SaveAsync(
                targetItems.Select(item => item.Model).ToList(),
                _songScriptsFolderPath,
                _backupRootPath,
                _enableSongScriptsBackup,
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
            StatusText = $"メタ情報追加完了: {successCount} 件";
            return;
        }

        StatusText = $"メタ情報追加完了: {successCount} 件成功, {failCount} 件失敗";
        string errorLines = string.Join(
            Environment.NewLine,
            saveResults
                .Where(saveResult => !saveResult.Success)
                .Select(saveResult => $"{Path.GetFileName(saveResult.SourceFilePath)}: {saveResult.ErrorMessage}"));
        _dialogService.ShowMessageBox(
            $"一部メタ情報追加に失敗しました。\n{errorLines}",
            "メタ情報追加エラー",
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
            ApplyBeatmapMatchState(item);
            StatusText = updated
                ? $"BeatSaverメタデータ取得完了: {item.MapId}"
                : $"BeatSaverメタデータ取得完了(更新なし): {item.MapId}";
        }
        else
        {
            StatusText = $"BeatSaverメタデータが見つかりませんでした: {item.MapId}";
        }
    }

    private async Task DownloadMissingBeatmapAsync(object? parameter)
    {
        if (parameter is SongScriptsManagerItemViewModel item)
        {
            await DownloadMissingBeatmapsCoreAsync(new[] { item });
        }
    }

    private async Task DownloadMissingBeatmapsCoreAsync(IEnumerable<SongScriptsManagerItemViewModel> sourceItems)
    {
        var targetItems = sourceItems
            .Where(item => item.CanDownloadMissingBeatmap && !string.IsNullOrWhiteSpace(item.MissingBeatmapMapId))
            .GroupBy(item => NormalizeMapId(item.MissingBeatmapMapId), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (targetItems.Count == 0)
        {
            StatusText = "譜面取得対象がありません";
            return;
        }

        LoadSettings();
        if (!EnsureCustomLevelsPathConfigured(showMessage: true))
        {
            return;
        }

        int successCount = 0;
        int skippedCount = 0;
        var failedMessages = new List<string>();
        var processedMapIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < targetItems.Count; index++)
        {
            var item = targetItems[index];
            string mapId = NormalizeMapId(item.MissingBeatmapMapId);
            if (string.IsNullOrWhiteSpace(mapId))
            {
                continue;
            }

            processedMapIds.Add(mapId);
            StatusText = targetItems.Count == 1
                ? $"譜面取得中: {mapId}..."
                : $"譜面取得中 ({index + 1}/{targetItems.Count}): {mapId}...";

            SongScriptsMissingBeatmapDownloadResult result = await _missingBeatmapDownloadService.DownloadMissingBeatmapAsync(
                mapId,
                _beatmapIndex,
                _customLevelsPath);

            if (result.Success)
            {
                successCount++;
                continue;
            }

            if (result.IsUnavailableOnBeatSaver || result.IsAlreadyLoadedLatestHash)
            {
                skippedCount++;
                StatusText = result.ErrorMessage;
                continue;
            }

            StatusText = $"譜面取得失敗: {mapId}";
            failedMessages.Add($"{mapId}: {result.ErrorMessage}");
        }

        if (successCount > 0)
        {
            await RefreshBeatmapIndexAsync(showProgressDialog: false);
        }
        else
        {
            foreach (string mapId in processedMapIds)
            {
                ApplyBeatmapMatchStateForMapId(mapId);
            }
        }

        if (targetItems.Count > 1 || successCount > 0)
        {
            StatusText = BuildDownloadSummaryText(targetItems.Count, successCount, skippedCount, failedMessages.Count);
        }

        if (failedMessages.Count > 0)
        {
            _dialogService.ShowMessageBox(
                $"譜面取得に失敗しました。\n{string.Join("\n", failedMessages)}",
                "譜面取得エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ApplyBeatmapMatchStateForMapId(string mapId)
    {
        string normalizedMapId = NormalizeMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalizedMapId))
        {
            return;
        }

        foreach (var item in Items.Where(item =>
                     string.Equals(NormalizeMapId(item.MapId), normalizedMapId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(NormalizeMapId(item.MissingBeatmapMapId), normalizedMapId, StringComparison.OrdinalIgnoreCase)))
        {
            ApplyBeatmapMatchState(item);
        }
    }

    private static string BuildDownloadSummaryText(int totalCount, int successCount, int skippedCount, int errorCount)
    {
        var parts = new List<string> { $"譜面取得完了: 成功 {successCount} 件 / 対象 {totalCount} 件" };
        if (skippedCount > 0)
        {
            parts.Add($"スキップ {skippedCount} 件");
        }

        if (errorCount > 0)
        {
            parts.Add($"エラー {errorCount} 件");
        }

        return string.Join(" / ", parts);
    }

    private async Task RefreshBeatmapIndexAsync(bool showProgressDialog)
    {
        await _scanSemaphore.WaitAsync();
        try
        {
            int scanGeneration = BeginBeatmapLookup();
            LoadSettings();

            StatusText = "SongDetailsCacheを確認中...";
            await _cacheService.EnsureInitializedAsync();

            CameraSongScriptCompatibleBeatmapIndex? beatmapIndex = null;
            if (showProgressDialog)
            {
                _dialogService.ShowProgressDialog("譜面フォルダを再読込中...", async progress =>
                {
                    beatmapIndex = await Task.Run(() => _beatmapIndexService.ScanByMapId(_customLevelsPath, _customWipLevelsPath, progress));
                });
            }
            else
            {
                beatmapIndex = await Task.Run(() => _beatmapIndexService.ScanByMapId(_customLevelsPath, _customWipLevelsPath));
            }

            if (scanGeneration != _scanGeneration)
            {
                return;
            }

            _beatmapIndex = beatmapIndex ?? new CameraSongScriptCompatibleBeatmapIndex();
            _isBeatmapMatchFinalized = false;

            foreach (var item in Items)
            {
                ApplyBeatmapMatchState(item);
            }

            if (_beatmapIndex.BeatmapFolders.Count == 0)
            {
                CompleteHashScan(scanGeneration, _beatmapIndex, "hash検索: 対象なし", 100);
                return;
            }

            StatusText = $"譜面参照更新完了: {Items.Count} 件 (ID検索結果を表示中)";
            SetHashScanPending(_beatmapIndex.BeatmapFolders.Count);
            StartHashScan(scanGeneration);
        }
        finally
        {
            _scanSemaphore.Release();
        }
    }

    private void UpdateBeatmapMatchState(SongScriptsManagerItemViewModel item)
    {
        ApplyBeatmapMatchState(item);

        if (!_isBeatmapMatchFinalized)
        {
            StatusText = IsHashScanRunning
                ? "譜面参照更新: ID検索結果を反映中 (hash検索中)"
                : "譜面参照更新: ID検索結果のみ反映";
            return;
        }

        int missingCount = Items.Count(entry => entry.CanDownloadMissingBeatmap);
        StatusText = $"譜面参照更新: 未取得候補 {missingCount} 件";
    }

    private void ApplyBeatmapMatchState(SongScriptsManagerItemViewModel item)
    {
        var matchedFolders = new Dictionary<string, CameraSongScriptCompatibleBeatmapIndexService.CompatibleBeatmapFolder>(StringComparer.OrdinalIgnoreCase);

        foreach (string mapId in GetSongScriptMapIdLookupKeys(item.Model))
        {
            if (_beatmapIndex.ByMapId.TryGetValue(mapId, out var beatmapFolders))
            {
                AddMatchedFolders(matchedFolders, beatmapFolders);
            }
        }

        string hash = NormalizeHash(item.Hash);
        if (!string.IsNullOrEmpty(hash) && _beatmapIndex.ByHash.TryGetValue(hash, out var hashMatchedFolders))
        {
            AddMatchedFolders(matchedFolders, hashMatchedFolders);
        }

        var matchedCustomLevels = matchedFolders.Values
            .Where(folder => folder.IsCustomLevels)
            .OrderBy(folder => folder.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(folder => folder.ToMatchedFolder())
            .ToList();
        var matchedCustomWipLevels = matchedFolders.Values
            .Where(folder => !folder.IsCustomLevels)
            .OrderBy(folder => folder.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(folder => folder.ToMatchedFolder())
            .ToList();

        string? missingBeatmapMapId = _isBeatmapMatchFinalized
            ? ResolveMissingBeatmapMapId(item, matchedCustomLevels.Count + matchedCustomWipLevels.Count > 0)
            : null;
        item.UpdateBeatmapMatchState(matchedCustomLevels, matchedCustomWipLevels, missingBeatmapMapId);
    }

    private string? ResolveMissingBeatmapMapId(SongScriptsManagerItemViewModel item, bool hasMatchedBeatmap)
    {
        if (!_cacheService.IsAvailable || hasMatchedBeatmap)
        {
            return null;
        }

        foreach (string mapId in GetSongScriptMapIdLookupKeys(item.Model))
        {
            if (_beatmapIndex.InstalledMapIds.Contains(mapId) || _missingBeatmapDownloadService.IsDownloadBlocked(mapId))
            {
                continue;
            }

            return mapId;
        }

        return null;
    }

    private void StartHashScan(int scanGeneration)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        _hashScanCancellationTokenSource = cancellationTokenSource;
        IsHashScanRunning = true;
        HashScanProgressValue = 0;
        HashScanStatusText = $"hash検索中... 0 / {_beatmapIndex.BeatmapFolders.Count}";
        _ = RunHashScanAsync(scanGeneration, cancellationTokenSource);
    }

    private async Task RunHashScanAsync(int scanGeneration, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            CameraSongScriptCompatibleBeatmapIndex finalIndex = await Task.Run(() => _beatmapIndexService.Scan(
                _customLevelsPath,
                _customWipLevelsPath,
                (message, percent) => UpdateHashScanProgress(scanGeneration, message, percent),
                cancellationTokenSource.Token),
                cancellationTokenSource.Token);

            if (cancellationTokenSource.IsCancellationRequested || scanGeneration != _scanGeneration)
            {
                return;
            }

            if (Application.Current == null)
            {
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CompleteHashScan(scanGeneration, finalIndex, "hash検索: 完了", 100);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (scanGeneration != _scanGeneration || Application.Current == null)
            {
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsHashScanRunning = false;
                HashScanStatusText = $"hash検索エラー: {ex.Message}";
                HashScanProgressValue = 0;
                StatusText = $"SongScripts読込完了: {Items.Count} 件 (hash検索失敗)";
            });
        }
        finally
        {
            if (ReferenceEquals(_hashScanCancellationTokenSource, cancellationTokenSource))
            {
                _hashScanCancellationTokenSource = null;
            }

            cancellationTokenSource.Dispose();
        }
    }

    private void UpdateHashScanProgress(int scanGeneration, string message, double? percent)
    {
        if (scanGeneration != _scanGeneration || Application.Current == null)
        {
            return;
        }

        _ = Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (scanGeneration != _scanGeneration)
            {
                return;
            }

            HashScanStatusText = message;
            HashScanProgressValue = percent ?? 0;
        });
    }

    private void CompleteHashScan(
        int scanGeneration,
        CameraSongScriptCompatibleBeatmapIndex beatmapIndex,
        string hashStatusText,
        double progressValue)
    {
        if (scanGeneration != _scanGeneration)
        {
            return;
        }

        _beatmapIndex = beatmapIndex;
        _isBeatmapMatchFinalized = true;
        IsHashScanRunning = false;
        HashScanStatusText = hashStatusText;
        HashScanProgressValue = progressValue;

        foreach (var item in Items)
        {
            ApplyBeatmapMatchState(item);
        }

        int missingCount = Items.Count(item => item.CanDownloadMissingBeatmap);
        StatusText = $"SongScripts読込完了: {Items.Count} 件 (未取得候補 {missingCount} 件)";
    }

    private int BeginBeatmapLookup()
    {
        CancelHashScan();
        _scanGeneration++;
        _isBeatmapMatchFinalized = false;
        SetHashScanIdle("hash検索: 準備中");
        return _scanGeneration;
    }

    private void CancelHashScan()
    {
        var cancellationTokenSource = _hashScanCancellationTokenSource;
        _hashScanCancellationTokenSource = null;
        cancellationTokenSource?.Cancel();
    }

    private void SetHashScanPending(int totalCount)
    {
        SetHashScanIdle($"hash検索: 開始待ち (0 / {totalCount})");
    }

    private void SetHashScanIdle(string statusText, double progressValue = 0)
    {
        IsHashScanRunning = false;
        HashScanStatusText = statusText;
        HashScanProgressValue = progressValue;
    }

    private static IEnumerable<string> GetSongScriptMapIdLookupKeys(SongScriptsManagerEntry entry)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderedKeys = new List<string>();
        AddMapIdKey(keys, orderedKeys, entry.MapId);
        AddMapIdKey(keys, orderedKeys, entry.PathMapId);
        return orderedKeys;
    }

    private static void AddMapIdKey(ISet<string> keys, ICollection<string> orderedKeys, string? mapId)
    {
        string normalized = NormalizeMapId(mapId);
        if (!string.IsNullOrEmpty(normalized) && keys.Add(normalized))
        {
            orderedKeys.Add(normalized);
        }
    }

    private static void AddMatchedFolders(
        IDictionary<string, CameraSongScriptCompatibleBeatmapIndexService.CompatibleBeatmapFolder> target,
        IEnumerable<CameraSongScriptCompatibleBeatmapIndexService.CompatibleBeatmapFolder> source)
    {
        foreach (var folder in source)
        {
            if (string.IsNullOrWhiteSpace(folder.FullPath))
            {
                continue;
            }

            target[folder.FullPath] = folder;
        }
    }

    private static string NormalizeMapId(string? mapId)
    {
        return string.IsNullOrWhiteSpace(mapId)
            ? string.Empty
            : mapId.Trim().ToLowerInvariant();
    }

    private static string NormalizeHash(string? hash)
    {
        return string.IsNullOrWhiteSpace(hash)
            ? string.Empty
            : hash.Trim().ToLowerInvariant();
    }
}
