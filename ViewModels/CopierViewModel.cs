using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CameraScriptManager.Models;
using CameraScriptManager.Services;

namespace CameraScriptManager.ViewModels;

public class CopierViewModel : ViewModelBase
{
    private readonly BeatMapScanner _scanner = new();
    private readonly OggDurationService _oggDurationService;
    private readonly ArchiveImportService _importService = new();
    private readonly SongDetailsCacheService _cacheService;
    private readonly BeatSaverApiClient _apiClient;
    private readonly SongScriptCopyService _copyService;
    private readonly CameraSongScriptCompatibleBeatmapIndexService _beatmapIndexService;
    private readonly SongScriptsMissingBeatmapDownloadService _missingBeatmapDownloadService;
    private readonly SettingsService _settingsService = new();

    private Dictionary<string, List<BeatMapFolder>> _customLevelsFolders = new();
    private Dictionary<string, List<BeatMapFolder>> _customWIPLevelsFolders = new();
    private string _backupRootPath = "";
    private bool _enableCopierBackup = true;

    public CopierViewModel()
    {
        _oggDurationService = new OggDurationService();
        _cacheService = new SongDetailsCacheService();
        _apiClient = new BeatSaverApiClient(_cacheService);
        _copyService = new SongScriptCopyService(_apiClient);
        _beatmapIndexService = new CameraSongScriptCompatibleBeatmapIndexService(_cacheService);
        _missingBeatmapDownloadService = new SongScriptsMissingBeatmapDownloadService(_apiClient);

        // SongDetailsCacheをバックグラウンドで初期化
        _ = _cacheService.InitAsync();

        // Register encoding provider for cp932
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Load settings
        var settings = _settingsService.Load();
        _customLevelsPath = settings.CustomLevelsPath;
        _customWIPLevelsPath = settings.CustomWIPLevelsPath;
        _addMetadata = settings.AddMetadata;
        _showMetadataColumns = settings.ShowMetadataColumns;
        _backupRootPath = BackupPathResolver.ResolveBackupRootPath(settings);
        _enableCopierBackup = settings.EnableCopierBackup;

        _defaultRenameOption = ParseDefaultRenameOption(settings);

        // Commands
        RescanCommand = new RelayCommand(() => RescanFolders(showMissingPathMessage: true));
        CopyCommand = new AsyncRelayCommand(ExecuteCopy, () => Entries.Count > 0 && !IsBusy);
        ClearEntriesCommand = new RelayCommand(() => Entries.Clear());
        OpenFilesCommand = new RelayCommand(OpenFiles);
        FetchBeatSaverMetadataCommand = new AsyncRelayCommand(ExecuteFetchBeatSaverMetadataAsync, CanFetchBeatSaverMetadata);
        DownloadMissingBeatmapCommand = new AsyncRelayCommand(DownloadMissingBeatmapAsync, CanDownloadMissingBeatmap);
        DownloadSelectedMissingBeatmapsCommand = new AsyncRelayCommand(DownloadSelectedMissingBeatmapsAsync, CanDownloadSelectedMissingBeatmaps);
        
        // Context Menu Commands
        DeleteSelectedCommand = new RelayCommand(ExecuteDeleteSelected);
        CopyIdsCommand = new RelayCommand(ExecuteCopyIds);
        RenameNoneCommand = new RelayCommand(p => ExecuteRenameOption(p, RenameOption.無し));
        RenameSongScriptCommand = new RelayCommand(p => ExecuteRenameOption(p, RenameOption.SongScript));
        RenameIdAuthorSongNameCommand = new RelayCommand(p => ExecuteRenameOption(p, RenameOption.IdAuthorSongName));
        CustomLevelsOnCommand = new RelayCommand(p => ExecuteSettingsToggle(p, "CL", true));
        CustomLevelsOffCommand = new RelayCommand(p => ExecuteSettingsToggle(p, "CL", false));
        CustomWipLevelsOnCommand = new RelayCommand(p => ExecuteSettingsToggle(p, "WIP", true));
        CustomWipLevelsOffCommand = new RelayCommand(p => ExecuteSettingsToggle(p, "WIP", false));
        OpenExplorerCLCommand = new RelayCommand(p => ExecuteOpenExplorer(p, "CL"));
        OpenExplorerWIPCommand = new RelayCommand(p => ExecuteOpenExplorer(p, "WIP"));
        SongNameSourceCommand = new AsyncRelayCommand(p => ExecuteSetSongNameOptionAsync(p, SongNameOption.Source));
        SongNameBeatSaverCommand = new AsyncRelayCommand(p => ExecuteSetSongNameOptionAsync(p, SongNameOption.BeatSaverSongName));
        SongNameBeatSaverAndAuthorCommand = new AsyncRelayCommand(p => ExecuteSetSongNameOptionAsync(p, SongNameOption.BeatSaverSongNameAndAuthor));

        // Initial scan
        RescanFolders(showMissingPathMessage: false);
    }

    // Settings
    private string _customLevelsPath = "";
    private string _customWIPLevelsPath = "";

    private bool _addMetadata = false;
    public bool AddMetadata
    {
        get => _addMetadata;
        set
        {
            if (SetProperty(ref _addMetadata, value))
                SaveSettings();
        }
    }

    private bool _showMetadataColumns = false;
    public bool ShowMetadataColumns
    {
        get => _showMetadataColumns;
        set
        {
            if (SetProperty(ref _showMetadataColumns, value))
                SaveSettings();
        }
    }

    private RenameOption _defaultRenameOption = RenameOption.SongScript;
    public RenameOption DefaultRenameOption
    {
        get => _defaultRenameOption;
        set
        {
            if (SetProperty(ref _defaultRenameOption, value))
                SaveSettings();
        }
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // SongDetailsCache デバッグ用インジケータ
    private bool? _lastCacheLookupSuccess;
    public bool? LastCacheLookupSuccess
    {
        get => _lastCacheLookupSuccess;
        set => SetProperty(ref _lastCacheLookupSuccess, value);
    }

    private int _cacheHitCount;
    public int CacheHitCount
    {
        get => _cacheHitCount;
        set => SetProperty(ref _cacheHitCount, value);
    }

    private int _cacheMissCount;
    public int CacheMissCount
    {
        get => _cacheMissCount;
        set => SetProperty(ref _cacheMissCount, value);
    }

    public ObservableCollection<SongScriptEntryViewModel> Entries { get; } = new();

    // Commands
    public ICommand RescanCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand ClearEntriesCommand { get; }
    public ICommand OpenFilesCommand { get; }
    public ICommand FetchBeatSaverMetadataCommand { get; }
    public ICommand DownloadMissingBeatmapCommand { get; }
    public ICommand DownloadSelectedMissingBeatmapsCommand { get; }

    // Context Menu Commands
    public ICommand DeleteSelectedCommand { get; }
    public ICommand CopyIdsCommand { get; }
    public ICommand RenameNoneCommand { get; }
    public ICommand RenameSongScriptCommand { get; }
    public ICommand RenameIdAuthorSongNameCommand { get; }
    public ICommand CustomLevelsOnCommand { get; }
    public ICommand CustomLevelsOffCommand { get; }
    public ICommand CustomWipLevelsOnCommand { get; }
    public ICommand CustomWipLevelsOffCommand { get; }
    public ICommand OpenExplorerCLCommand { get; }
    public ICommand OpenExplorerWIPCommand { get; }
    public ICommand SongNameSourceCommand { get; }
    public ICommand SongNameBeatSaverCommand { get; }
    public ICommand SongNameBeatSaverAndAuthorCommand { get; }

    public void ReloadSettings()
    {
        var settings = _settingsService.Load();
        bool pathChanged = _customLevelsPath != settings.CustomLevelsPath || 
                           _customWIPLevelsPath != settings.CustomWIPLevelsPath;
                           
        _customLevelsPath = settings.CustomLevelsPath;
        _customWIPLevelsPath = settings.CustomWIPLevelsPath;
        _addMetadata = settings.AddMetadata;
        ShowMetadataColumns = settings.ShowMetadataColumns;
        _backupRootPath = BackupPathResolver.ResolveBackupRootPath(settings);
        _enableCopierBackup = settings.EnableCopierBackup;

        _defaultRenameOption = ParseDefaultRenameOption(settings);

        if (pathChanged)
        {
            RescanFolders(showMissingPathMessage: false);
        }
    }

    private bool EnsureAnyBeatmapPathConfigured(bool showMessage)
    {
        if (!string.IsNullOrWhiteSpace(_customLevelsPath) || !string.IsNullOrWhiteSpace(_customWIPLevelsPath))
        {
            return true;
        }

        StatusMessage = "CustomLevelsまたはCustomWIPLevelsパスが設定されていません";
        if (showMessage)
        {
            MessageBox.Show(
                "SettingsでCustomLevelsまたはCustomWIPLevelsパスを設定して下さい。",
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

        StatusMessage = "CustomLevelsパスが設定されていません";
        if (showMessage)
        {
            MessageBox.Show(
                "SettingsでCustomLevelsパスを設定して下さい。",
                "情報",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return false;
    }

    private void OpenFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            DefaultExt = ".json",
            Filter = "サポートされるファイル (*.json;*.zip;*.7z;*.rar;*.tar;*.gz)|*.json;*.zip;*.7z;*.rar;*.tar;*.gz|JSON ファイル (*.json)|*.json|アーカイブ (*.zip;*.7z;*.rar;*.tar;*.gz)|*.zip;*.7z;*.rar;*.tar;*.gz|すべてのファイル (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            HandleDroppedFiles(dialog.FileNames);
        }
    }

    public void RescanFolders(bool showMissingPathMessage)
    {
        bool hasAnyPath = EnsureAnyBeatmapPathConfigured(showMissingPathMessage);

        _customLevelsFolders = string.IsNullOrWhiteSpace(_customLevelsPath)
            ? new Dictionary<string, List<BeatMapFolder>>()
            : _scanner.ScanFolder(_customLevelsPath, true);
        _customWIPLevelsFolders = string.IsNullOrWhiteSpace(_customWIPLevelsPath)
            ? new Dictionary<string, List<BeatMapFolder>>()
            : _scanner.ScanFolder(_customWIPLevelsPath, false);

        if (hasAnyPath)
        {
            int clCount = _customLevelsFolders.Values.Sum(v => v.Count);
            int wipCount = _customWIPLevelsFolders.Values.Sum(v => v.Count);
            StatusMessage = $"スキャン完了: CustomLevels {clCount} フォルダ, CustomWIPLevels {wipCount} フォルダ";
        }

        // Re-match existing entries
        foreach (var entry in Entries)
        {
            entry.UpdateMatchedFolders(_customLevelsFolders, _customWIPLevelsFolders);
            UpdateOggDuration(entry);
            UpdateBeatmapDownloadAvailability(entry);
        }
    }

    private static RenameOption ParseDefaultRenameOption(AppSettings settings)
    {
        return Enum.TryParse<RenameOption>(settings.DefaultRenameOption, out var parsed)
            ? parsed
            : RenameOption.SongScript;
    }

    public void HandleDroppedFiles(string[] filePaths)
    {
        int importedCount = 0;
#if DEBUG
        DebugLog($"HandleDroppedFiles: cacheService.IsAvailable={_cacheService.IsAvailable}, files={filePaths.Length}");
#endif

        foreach (var filePath in filePaths)
        {
            var entries = _importService.ImportFile(filePath);

            foreach (var entry in entries)
            {
                entry.RenameChoice = _defaultRenameOption;

                // Match folders
                string key = entry.HexId.ToLowerInvariant();
                if (_customLevelsFolders.TryGetValue(key, out var clList))
                {
                    entry.MatchedCustomLevels = clList;
                    entry.CopyToCustomLevels = true;
                    entry.SelectedCustomLevelsFolder = clList[0];
                }
                if (_customWIPLevelsFolders.TryGetValue(key, out var wipList))
                {
                    entry.MatchedCustomWIPLevels = wipList;
                    entry.CopyToCustomWIPLevels = true;
                    entry.SelectedCustomWIPLevelsFolder = wipList[0];
                }

                var vm = new SongScriptEntryViewModel(entry);
                vm.OnHexIdChanged = OnEntryHexIdChangedAsync;
                UpdateOggDuration(vm);
                UpdateBeatmapDownloadAvailability(vm);

                Entries.Add(vm);
                importedCount++;
            }
        }

        StatusMessage = $"{importedCount} 件のSongScriptを読み込みました";
    }

    private async Task OnEntryHexIdChangedAsync(SongScriptEntryViewModel entry)
    {
        string hexId = entry.HexId.ToLowerInvariant();
        entry.Model.Metadata = null;
        if (entry.SongNameChoice != SongNameOption.Source)
        {
            entry.UpdateSongName();
        }

        // フォルダ再マッチ
        entry.UpdateMatchedFolders(_customLevelsFolders, _customWIPLevelsFolders);
        UpdateOggDuration(entry);
        UpdateBeatmapDownloadAvailability(entry);
        StatusMessage = string.IsNullOrWhiteSpace(hexId)
            ? "ID未設定の行を更新しました"
            : $"ID {hexId} の照合結果を更新しました";
    }

    public async Task FetchApiDataAsync(SongScriptEntryViewModel entry)
    {
        string hexId = NormalizeMapId(entry.HexId);
        if (string.IsNullOrEmpty(hexId))
        {
            return;
        }

        try
        {
            BeatSaverApiResponse? apiResponse = await GetBeatSaverApiResponseAsync(hexId);
            entry.Model.Metadata = apiResponse?.Metadata;
            entry.UpdateSongName();
            StatusMessage = apiResponse?.Metadata != null
                ? $"ID {hexId} のメタデータを取得しました"
                : $"ID {hexId} のメタデータが見つかりませんでした";
        }
        catch
        {
            entry.Model.Metadata = null;
            entry.UpdateSongName();
        }
    }

    private bool CanFetchBeatSaverMetadata(object? parameter)
    {
        return GetSelectedEntries(parameter).Any(entry => !string.IsNullOrWhiteSpace(entry.HexId));
    }

    private async Task ExecuteFetchBeatSaverMetadataAsync(object? parameter)
    {
        var targetGroups = GetSelectedEntries(parameter)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.HexId))
            .GroupBy(entry => NormalizeMapId(entry.HexId), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToList();

        if (targetGroups.Count == 0)
        {
            StatusMessage = "BeatSaverからmetadata取得する項目がありません";
            return;
        }

        int totalCount = targetGroups.Sum(group => group.Count());
        int successCount = 0;
        int notFoundCount = 0;
        int errorCount = 0;
        var failedMessages = new List<string>();

        foreach (var group in targetGroups)
        {
            string mapId = group.Key;

            try
            {
                BeatSaverApiResponse? apiResponse = await GetBeatSaverApiResponseAsync(mapId);
                if (apiResponse?.Metadata == null)
                {
                    foreach (SongScriptEntryViewModel entry in group)
                    {
                        entry.Model.Metadata = null;
                    }

                    notFoundCount += group.Count();
                    StatusMessage = $"BeatSaverメタデータが見つかりませんでした: {mapId}";
                    continue;
                }

                foreach (SongScriptEntryViewModel entry in group)
                {
                    entry.ApplyBeatSaverData(apiResponse);
                }

                successCount += group.Count();
                StatusMessage = $"BeatSaverメタデータ取得完了: {mapId}";
            }
            catch (Exception ex)
            {
                errorCount += group.Count();
                failedMessages.Add($"{mapId}: {ex.Message}");
                StatusMessage = $"BeatSaverメタデータ取得エラー: {mapId}";
            }
        }

        if (totalCount > 1)
        {
            StatusMessage = BuildBeatSaverFetchSummaryText(totalCount, successCount, notFoundCount, errorCount);
        }

        if (failedMessages.Count > 0)
        {
            MessageBox.Show(
                $"BeatSaverメタデータ取得に失敗しました。\n{string.Join("\n", failedMessages)}",
                "BeatSaverメタデータ取得エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private IList<SongScriptEntryViewModel> GetSelectedEntries(object? parameter)
    {
        if (parameter is System.Collections.IList list)
        {
            return list.Cast<SongScriptEntryViewModel>().ToList();
        }
        return new List<SongScriptEntryViewModel>();
    }

    private async Task<BeatSaverApiResponse?> GetBeatSaverApiResponseAsync(string hexId)
    {
        StatusMessage = $"BeatSaver API取得中: {hexId}...";
        var (apiResponse, _, cacheHit) = await _apiClient.GetMapAsync(hexId);
        if (cacheHit.HasValue)
        {
            LastCacheLookupSuccess = cacheHit.Value;
            if (cacheHit.Value)
                CacheHitCount++;
            else
                CacheMissCount++;
        }

        return apiResponse;
    }

    private void ExecuteDeleteSelected(object? parameter)
    {
        var selected = GetSelectedEntries(parameter);
        foreach (var entry in selected)
            Entries.Remove(entry);
    }

    private void ExecuteCopyIds(object? parameter)
    {
        var selected = GetSelectedEntries(parameter);
        if (selected.Count == 0) return;

        var ids = selected
            .Where(i => !string.IsNullOrWhiteSpace(i.HexId))
            .Select(i => i.HexId)
            .Distinct();
            
        string text = string.Join("\r\n", ids);
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
        }
    }

    private void ExecuteRenameOption(object? parameter, RenameOption option)
    {
        foreach (var entry in GetSelectedEntries(parameter))
            entry.RenameChoice = option;
    }

    private void ExecuteSettingsToggle(object? parameter, string type, bool state)
    {
        foreach (var entry in GetSelectedEntries(parameter))
        {
            if (type == "CL") 
            {
                if (state && entry.CanCopyToCustomLevels) entry.CopyToCustomLevels = true;
                else if (!state) entry.CopyToCustomLevels = false;
            }
            else if (type == "WIP")
            {
                if (state && entry.CanCopyToCustomWIPLevels) entry.CopyToCustomWIPLevels = true;
                else if (!state) entry.CopyToCustomWIPLevels = false;
            }
        }
    }

    private void ExecuteOpenExplorer(object? parameter, string type)
    {
        foreach (var entry in GetSelectedEntries(parameter))
        {
            var folder = type == "CL" ? entry.SelectedCustomLevelsFolder : entry.SelectedCustomWIPLevelsFolder;
            if (folder != null && Directory.Exists(folder.FullPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folder.FullPath) { UseShellExecute = true });
            }
        }
    }

    private async Task ExecuteSetSongNameOptionAsync(object? parameter, SongNameOption option)
    {
        var selectedEntries = GetSelectedEntries(parameter);
        foreach (var entry in selectedEntries)
        {
            entry.SongNameChoice = option;
            
            if ((option == SongNameOption.BeatSaverSongName || option == SongNameOption.BeatSaverSongNameAndAuthor) && 
                entry.Model.Metadata == null)
            {
                await FetchApiDataAsync(entry);
            }
        }
    }

    private bool CanDownloadMissingBeatmap(object? parameter)
    {
        return parameter is SongScriptEntryViewModel entry && entry.CanDownloadMissingBeatmap;
    }

    private bool CanDownloadSelectedMissingBeatmaps(object? parameter)
    {
        return GetSelectedEntries(parameter).Any(entry => entry.CanDownloadMissingBeatmap);
    }

    private async Task DownloadMissingBeatmapAsync(object? parameter)
    {
        if (parameter is SongScriptEntryViewModel entry)
        {
            await DownloadMissingBeatmapsCoreAsync(new[] { entry });
        }
    }

    private Task DownloadSelectedMissingBeatmapsAsync(object? parameter)
    {
        return DownloadMissingBeatmapsCoreAsync(GetSelectedEntries(parameter));
    }

    private async Task DownloadMissingBeatmapsCoreAsync(IEnumerable<SongScriptEntryViewModel> sourceEntries)
    {
        var targetEntries = sourceEntries
            .Where(entry => entry.CanDownloadMissingBeatmap && !string.IsNullOrWhiteSpace(entry.HexId))
            .GroupBy(entry => NormalizeMapId(entry.HexId), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (targetEntries.Count == 0)
        {
            StatusMessage = "譜面取得対象がありません";
            return;
        }

        if (!EnsureCustomLevelsPathConfigured(showMessage: true))
        {
            return;
        }

        StatusMessage = targetEntries.Count == 1
            ? $"譜面取得準備中: {targetEntries[0].HexId.Trim()}..."
            : $"譜面取得準備中... 0 / {targetEntries.Count}";
        await _cacheService.EnsureInitializedAsync();

        CameraSongScriptCompatibleBeatmapIndex beatmapIndex = await Task.Run(() =>
            _beatmapIndexService.Scan(_customLevelsPath, _customWIPLevelsPath));

        int successCount = 0;
        int skippedCount = 0;
        var failedMessages = new List<string>();
        var processedMapIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < targetEntries.Count; index++)
        {
            SongScriptEntryViewModel entry = targetEntries[index];
            string mapId = NormalizeMapId(entry.HexId);
            if (string.IsNullOrWhiteSpace(mapId))
            {
                continue;
            }

            processedMapIds.Add(mapId);
            StatusMessage = targetEntries.Count == 1
                ? $"譜面取得中: {mapId}..."
                : $"譜面取得中 ({index + 1}/{targetEntries.Count}): {mapId}...";

            SongScriptsMissingBeatmapDownloadResult result = await _missingBeatmapDownloadService.DownloadMissingBeatmapAsync(
                mapId,
                beatmapIndex,
                _customLevelsPath);

            if (result.Success)
            {
                successCount++;
                continue;
            }

            if (result.IsUnavailableOnBeatSaver || result.IsAlreadyLoadedLatestHash)
            {
                skippedCount++;
                StatusMessage = result.ErrorMessage;
                continue;
            }

            StatusMessage = $"譜面取得失敗: {mapId}";
            failedMessages.Add($"{mapId}: {result.ErrorMessage}");
        }

        if (successCount > 0)
        {
            RescanFolders(showMissingPathMessage: false);
        }
        else
        {
            foreach (string mapId in processedMapIds)
            {
                UpdateBeatmapDownloadAvailabilityForMapId(mapId);
            }
        }

        if (targetEntries.Count > 1 || successCount > 0)
        {
            StatusMessage = BuildDownloadSummaryText(targetEntries.Count, successCount, skippedCount, failedMessages.Count);
        }

        if (failedMessages.Count > 0)
        {
            MessageBox.Show(
                $"譜面取得に失敗しました。\n{string.Join("\n", failedMessages)}",
                "譜面取得エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void UpdateBeatmapDownloadAvailabilityForMapId(string mapId)
    {
        string normalizedMapId = NormalizeMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalizedMapId))
        {
            return;
        }

        foreach (var entry in Entries.Where(entry =>
                     string.Equals(NormalizeMapId(entry.HexId), normalizedMapId, StringComparison.OrdinalIgnoreCase)))
        {
            UpdateBeatmapDownloadAvailability(entry);
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

    private static string BuildBeatSaverFetchSummaryText(int totalCount, int successCount, int notFoundCount, int errorCount)
    {
        var parts = new List<string> { $"BeatSaverメタデータ取得完了: 成功 {successCount} 件 / 対象 {totalCount} 件" };
        if (notFoundCount > 0)
        {
            parts.Add($"未検出 {notFoundCount} 件");
        }

        if (errorCount > 0)
        {
            parts.Add($"エラー {errorCount} 件");
        }

        return string.Join(" / ", parts);
    }

    private static string NormalizeMapId(string? mapId)
    {
        return string.IsNullOrWhiteSpace(mapId)
            ? string.Empty
            : mapId.Trim().ToLowerInvariant();
    }

    private void UpdateOggDuration(SongScriptEntryViewModel vm)
    {
        string? folderPath = vm.SelectedCustomLevelsFolder?.FullPath 
                          ?? vm.SelectedCustomWIPLevelsFolder?.FullPath;
        
        if (folderPath != null)
        {
            vm.Model.OggDuration = _oggDurationService.GetDurationFromFolder(folderPath);
        }
        else
        {
            vm.Model.OggDuration = 0;
        }
        vm.NotifyOggDurationChanged();
    }

    private void UpdateBeatmapDownloadAvailability(SongScriptEntryViewModel entry)
    {
        bool hasMatchedFolder = entry.Model.MatchedCustomLevels.Count > 0 || entry.Model.MatchedCustomWIPLevels.Count > 0;
        bool canDownload = !string.IsNullOrWhiteSpace(entry.HexId) &&
            !hasMatchedFolder &&
            !_missingBeatmapDownloadService.IsDownloadBlocked(entry.HexId);
        entry.UpdateBeatmapDownloadAvailability(canDownload);
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task ExecuteCopy()
    {
        // Check for overwrite warnings
        var overwriteEntries = Entries.Where(e => e.HasOverwriteWarning &&
            (e.CopyToCustomLevels || e.CopyToCustomWIPLevels)).ToList();

        if (overwriteEntries.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("以下のファイルが上書きされます:");
            sb.AppendLine();
            foreach (var e in overwriteEntries)
            {
                if (!string.IsNullOrEmpty(e.OverwriteDetails))
                    sb.AppendLine($"  {e.HexId}: {e.OverwriteDetails}");
            }
            sb.AppendLine();
            sb.AppendLine("続行しますか?");

            var result = MessageBox.Show(sb.ToString(), "上書き確認",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
        }

        var entriesToCopy = Entries
            .Where(e => e.CopyToCustomLevels || e.CopyToCustomWIPLevels)
            .Select(e => e.Model)
            .ToList();

        if (entriesToCopy.Count == 0)
        {
            StatusMessage = "コピー対象がありません";
            return;
        }

        IsBusy = true;
        try
        {
            var progress = new Progress<string>(msg => StatusMessage = msg);
            var results = await _copyService.CopyAllAsync(
                entriesToCopy,
                _addMetadata,
                _enableCopierBackup,
                _backupRootPath,
                _customLevelsPath,
                _customWIPLevelsPath,
                progress);

            int successCount = results.Count(r => r.Success);
            int failCount = results.Count(r => !r.Success);
            int overwriteCount = results.Count(r => r.WasOverwrite);

            var summary = new StringBuilder();
            summary.AppendLine($"コピー完了: {successCount} 成功");
            if (overwriteCount > 0) summary.AppendLine($"  うち {overwriteCount} 件上書き");
            if (failCount > 0)
            {
                summary.AppendLine($"  {failCount} 件失敗:");
                foreach (var fail in results.Where(r => !r.Success))
                    summary.AppendLine($"    {fail.HexId}: {fail.ErrorMessage}");
            }

            StatusMessage = $"コピー完了: {successCount} 成功, {failCount} 失敗";
            MessageBox.Show(summary.ToString(), "コピー結果", MessageBoxButton.OK,
                failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            // Refresh overwrite warnings
            foreach (var entry in Entries)
                entry.UpdateOverwriteWarnings();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SaveSettings()
    {
        var currentSettings = _settingsService.Load();
        currentSettings.AddMetadata = AddMetadata;
        currentSettings.DefaultRenameOption = DefaultRenameOption.ToString();
        currentSettings.ShowMetadataColumns = ShowMetadataColumns;
        _settingsService.Save(currentSettings);
    }

#if DEBUG
    private static void DebugLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [CopierVM] {message}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!System.IO.Directory.Exists(logDir))
                System.IO.Directory.CreateDirectory(logDir);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(logDir, "debug_songdetails.log"),
                line + Environment.NewLine);
        }
        catch { }
    }
#endif
}
