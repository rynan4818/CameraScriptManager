using System.Collections.ObjectModel;
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
    private readonly ZipImportService _importService = new();
    private readonly BeatSaverApiClient _apiClient = new();
    private readonly SongScriptCopyService _copyService;
    private readonly SettingsService _settingsService = new();

    private Dictionary<string, List<BeatMapFolder>> _customLevelsFolders = new();
    private Dictionary<string, List<BeatMapFolder>> _customWIPLevelsFolders = new();
    private readonly OggDurationService _oggDurationService;

    public CopierViewModel()
    {
        _oggDurationService = new OggDurationService();
        _copyService = new SongScriptCopyService(_apiClient);

        // Register encoding provider for cp932
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Load settings
        var settings = _settingsService.Load();
        _customLevelsPath = settings.CustomLevelsPath;
        _customWIPLevelsPath = settings.CustomWIPLevelsPath;
        _addMetadata = settings.AddMetadata;

        // 旧設定からのマイグレーション
        if (settings.DefaultRenameToAuthorIdSongName == true)
            _defaultRenameOption = RenameOption.AuthorIdSongName;
        else if (Enum.TryParse<RenameOption>(settings.DefaultRenameOption, out var parsed))
            _defaultRenameOption = parsed;
        else
            _defaultRenameOption = RenameOption.SongScript;

        // Commands
        BrowseCustomLevelsCommand = new RelayCommand(BrowseCustomLevels);
        BrowseCustomWIPLevelsCommand = new RelayCommand(BrowseCustomWIPLevels);
        RescanCommand = new RelayCommand(RescanFolders);
        CopyCommand = new AsyncRelayCommand(ExecuteCopy, () => Entries.Count > 0 && !IsBusy);
        ClearEntriesCommand = new RelayCommand(() => Entries.Clear());
        OpenFilesCommand = new RelayCommand(OpenFiles);

        // Initial scan
        RescanFolders();
    }

    // Settings
    private string _customLevelsPath = "";
    public string CustomLevelsPath
    {
        get => _customLevelsPath;
        set
        {
            if (SetProperty(ref _customLevelsPath, value))
                SaveSettings();
        }
    }

    private string _customWIPLevelsPath = "";
    public string CustomWIPLevelsPath
    {
        get => _customWIPLevelsPath;
        set
        {
            if (SetProperty(ref _customWIPLevelsPath, value))
                SaveSettings();
        }
    }

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

    public ObservableCollection<SongScriptEntryViewModel> Entries { get; } = new();

    // Commands
    public ICommand BrowseCustomLevelsCommand { get; }
    public ICommand BrowseCustomWIPLevelsCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand ClearEntriesCommand { get; }
    public ICommand OpenFilesCommand { get; }

    private void BrowseCustomLevels()
    {
        var path = BrowseFolder("CustomLevels フォルダを選択", CustomLevelsPath);
        if (path != null)
        {
            CustomLevelsPath = path;
            RescanFolders();
        }
    }

    private void BrowseCustomWIPLevels()
    {
        var path = BrowseFolder("CustomWIPLevels フォルダを選択", CustomWIPLevelsPath);
        if (path != null)
        {
            CustomWIPLevelsPath = path;
            RescanFolders();
        }
    }

    private static string? BrowseFolder(string description, string initialPath)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = description,
            InitialDirectory = Directory.Exists(initialPath) ? initialPath : ""
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private void OpenFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "SongScriptファイルを選択",
            Filter = "サポートされるファイル (*.json;*.zip)|*.json;*.zip|JSON ファイル (*.json)|*.json|ZIP ファイル (*.zip)|*.zip|すべてのファイル (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            HandleDroppedFiles(dialog.FileNames);
        }
    }

    public void RescanFolders()
    {
        _customLevelsFolders = _scanner.ScanFolder(CustomLevelsPath, true);
        _customWIPLevelsFolders = _scanner.ScanFolder(CustomWIPLevelsPath, false);

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

        foreach (var filePath in filePaths)
        {
            var entries = _importService.ImportFile(filePath);

            foreach (var entry in entries)
            {
                // Set default rename option
                entry.RenameChoice = DefaultRenameOption;

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
                Entries.Add(vm);
                importedCount++;
            }
        }

        StatusMessage = $"{importedCount} 件のSongScriptを読み込みました";
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
            var apiResponse = await _apiClient.GetMapAsync(hexId);
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
            var results = await _copyService.CopyAllAsync(entriesToCopy, AddMetadata, progress);

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
        _settingsService.Save(new AppSettings
        {
            CustomLevelsPath = CustomLevelsPath,
            CustomWIPLevelsPath = CustomWIPLevelsPath,
            AddMetadata = AddMetadata,
            DefaultRenameOption = DefaultRenameOption.ToString()
        });
    }
}
