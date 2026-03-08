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
    private readonly SettingsService _settingsService = new();

    private Dictionary<string, List<BeatMapFolder>> _customLevelsFolders = new();
    private Dictionary<string, List<BeatMapFolder>> _customWIPLevelsFolders = new();

    public CopierViewModel()
    {
        _oggDurationService = new OggDurationService();
        _cacheService = new SongDetailsCacheService();
        _apiClient = new BeatSaverApiClient(_cacheService);
        _copyService = new SongScriptCopyService(_apiClient);

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

        // 旧設定からのマイグレーション
        if (settings.DefaultRenameToAuthorIdSongName == true)
            _defaultRenameOption = RenameOption.AuthorIdSongName;
        else if (Enum.TryParse<RenameOption>(settings.DefaultRenameOption, out var parsed))
            _defaultRenameOption = parsed;
        else
            _defaultRenameOption = RenameOption.カスタム;

        // Commands
        RescanCommand = new RelayCommand(RescanFolders);
        CopyCommand = new AsyncRelayCommand(ExecuteCopy, () => Entries.Count > 0 && !IsBusy);
        ClearEntriesCommand = new RelayCommand(() => Entries.Clear());
        OpenFilesCommand = new RelayCommand(OpenFiles);
        
        // Context Menu Commands
        DeleteSelectedCommand = new RelayCommand(ExecuteDeleteSelected);
        CopyIdsCommand = new RelayCommand(ExecuteCopyIds);
        RenameNoneCommand = new RelayCommand(p => ExecuteRenameOption(p, RenameOption.無し));
        RenameSongScriptCommand = new RelayCommand(p => ExecuteRenameOption(p, RenameOption.SongScript));
        RenameAuthorIdSongNameCommand = new RelayCommand(p => ExecuteRenameOption(p, RenameOption.AuthorIdSongName));
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
        RescanFolders();
    }

    // Settings
    private string _customLevelsPath = "";
    private string _customWIPLevelsPath = "";

    private bool _addMetadata = true;
    public bool AddMetadata
    {
        get => _addMetadata;
        set
        {
            if (SetProperty(ref _addMetadata, value))
                SaveSettings();
        }
    }

    private bool _showMetadataColumns = true;
    public bool ShowMetadataColumns
    {
        get => _showMetadataColumns;
        set
        {
            if (SetProperty(ref _showMetadataColumns, value))
                SaveSettings();
        }
    }

    private RenameOption _defaultRenameOption = RenameOption.カスタム;
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

    // Context Menu Commands
    public ICommand DeleteSelectedCommand { get; }
    public ICommand CopyIdsCommand { get; }
    public ICommand RenameNoneCommand { get; }
    public ICommand RenameSongScriptCommand { get; }
    public ICommand RenameAuthorIdSongNameCommand { get; }
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

        if (settings.DefaultRenameToAuthorIdSongName == true)
            _defaultRenameOption = RenameOption.AuthorIdSongName;
        else if (Enum.TryParse<RenameOption>(settings.DefaultRenameOption, out var parsed))
            _defaultRenameOption = parsed;
        else
            _defaultRenameOption = RenameOption.カスタム;

        if (pathChanged)
        {
            RescanFolders();
        }
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

    public void RescanFolders()
    {
        if (string.IsNullOrEmpty(_customLevelsPath) && string.IsNullOrEmpty(_customWIPLevelsPath))
            return;

        _customLevelsFolders = _scanner.ScanFolder(_customLevelsPath, true);
        _customWIPLevelsFolders = _scanner.ScanFolder(_customWIPLevelsPath, false);

        int clCount = _customLevelsFolders.Values.Sum(v => v.Count);
        int wipCount = _customWIPLevelsFolders.Values.Sum(v => v.Count);
        StatusMessage = $"スキャン完了: CustomLevels {clCount} フォルダ, CustomWIPLevels {wipCount} フォルダ";

        // Re-match existing entries
        foreach (var entry in Entries)
        {
            entry.UpdateMatchedFolders(_customLevelsFolders, _customWIPLevelsFolders);
            UpdateOggDuration(entry);
        }
    }

    public void HandleDroppedFiles(string[] filePaths)
    {
        int importedCount = 0;
        var importedVms = new List<SongScriptEntryViewModel>();
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

                // metadataがないエントリはSongDetailsCacheから即座に補完を試みる
                if (!entry.HasOriginalMetadata && !string.IsNullOrEmpty(entry.HexId))
                {
#if DEBUG
                    DebugLog($"HandleDroppedFiles: trying cache for HexId=\"{entry.HexId}\"");
#endif
                    if (_cacheService.TryGetByMapId(entry.HexId, out var cacheResponse) && cacheResponse.Metadata != null)
                    {
                        entry.Metadata = cacheResponse.Metadata;
                        vm.UpdateFromCacheMetadata(cacheResponse.Metadata);
                        LastCacheLookupSuccess = true;
                        CacheHitCount++;
                    }
                    else
                    {
                        // キャッシュ未初期化 or ヒットしなかった場合、非同期補完リストに追加
#if DEBUG
                        DebugLog($"HandleDroppedFiles: cache MISS for HexId=\"{entry.HexId}\", queued for async fallback");
#endif
                        LastCacheLookupSuccess = false;
                        CacheMissCount++;
                        importedVms.Add(vm);
                    }
                }

                Entries.Add(vm);
                importedCount++;
            }
        }

        StatusMessage = $"{importedCount} 件のSongScriptを読み込みました";

        // キャッシュでカバーできなかったエントリを非同期でBeatSaver APIから補完
        if (importedVms.Count > 0)
        {
#if DEBUG
            DebugLog($"HandleDroppedFiles: {importedVms.Count} cache misses, starting PopulateMetadataAsync");
#endif
            _ = PopulateMetadataAsync(importedVms);
        }
    }

    private async Task PopulateMetadataAsync(List<SongScriptEntryViewModel> entries)
    {
        // キャッシュの初期化完了を待機してから問い合わせを開始
        // （HandleDroppedFiles時点では初期化未完了でキャッシュミスになるため）
#if DEBUG
        DebugLog($"PopulateMetadataAsync: waiting for cache init... IsAvailable={_cacheService.IsAvailable}");
#endif
        await _cacheService.EnsureInitializedAsync();
#if DEBUG
        DebugLog($"PopulateMetadataAsync: cache init done. IsAvailable={_cacheService.IsAvailable}. Processing {entries.Count} entries...");
#endif

        foreach (var vm in entries)
        {
            if (!vm.Model.HasOriginalMetadata && !string.IsNullOrEmpty(vm.HexId) && vm.Model.Metadata == null)
            {
                await FetchApiDataAsync(vm);
                if (vm.Model.Metadata != null)
                {
                    vm.UpdateFromCacheMetadata(vm.Model.Metadata);
                }
            }
        }
    }

    private async Task OnEntryHexIdChangedAsync(SongScriptEntryViewModel entry)
    {
        string hexId = entry.HexId.ToLowerInvariant();

        await FetchApiDataAsync(entry);

        // フォルダ再マッチ
        entry.UpdateMatchedFolders(_customLevelsFolders, _customWIPLevelsFolders);
        UpdateOggDuration(entry);
        StatusMessage = $"ID {hexId} の情報を更新しました";
    }

    public async Task FetchApiDataAsync(SongScriptEntryViewModel entry)
    {
        string hexId = entry.HexId.ToLowerInvariant();
        if (string.IsNullOrEmpty(hexId)) return;

        try
        {
            StatusMessage = $"API取得中: {hexId}...";
            var (apiResponse, fromApi, cacheHit) = await _apiClient.GetMapAsync(hexId);
            if (cacheHit.HasValue)
            {
                LastCacheLookupSuccess = cacheHit.Value;
                if (cacheHit.Value)
                    CacheHitCount++;
                else
                    CacheMissCount++;
            }
            if (apiResponse != null)
            {
                entry.Model.Metadata = apiResponse.Metadata;
            }
            else
            {
                entry.Model.Metadata = null;
            }
            entry.UpdateSongName();
            StatusMessage = $"ID {hexId} のメタデータを取得しました";
        }
        catch
        {
            entry.UpdateSongName();
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
            var settings = _settingsService.Load();
            var results = await _copyService.CopyAllAsync(entriesToCopy, _addMetadata, settings.CreateBackup, progress);

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
