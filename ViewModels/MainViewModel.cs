using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CameraScriptManager.Services;
using Microsoft.Win32;

namespace CameraScriptManager.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService = new();
    private readonly CameraScriptScanner _scanner = new();
    private readonly BeatSaverApiClient _apiClient = new();
    private string _customLevelsPath = "";
    private string _customWIPLevelsPath = "";
    private string _originalScriptPath1 = "";
    private string _originalScriptPath2 = "";
    private string _originalScriptPath3 = "";
    private string _statusText = "";

    public MainViewModel()
    {
        Items = new ObservableCollection<CameraScriptItemViewModel>();

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        AddMetadataCommand = new AsyncRelayCommand(AddMetadataAsync);
        ExportZipCommand = new AsyncRelayCommand(ExportZipAsync);
        FindOriginalScriptsCommand = new AsyncRelayCommand(FindOriginalScriptsAsync);
        BrowseCustomLevelsCommand = new RelayCommand(BrowseCustomLevels);
        BrowseCustomWIPLevelsCommand = new RelayCommand(BrowseCustomWIPLevels);
        BrowseOriginalScript1Command = new RelayCommand(BrowseOriginalScript1);
        BrowseOriginalScript2Command = new RelayCommand(BrowseOriginalScript2);
        BrowseOriginalScript3Command = new RelayCommand(BrowseOriginalScript3);

        LoadSettings();
    }

    public ObservableCollection<CameraScriptItemViewModel> Items { get; }

    public string CustomLevelsPath
    {
        get => _customLevelsPath;
        set
        {
            if (SetProperty(ref _customLevelsPath, value))
                SaveSettings();
        }
    }

    public string CustomWIPLevelsPath
    {
        get => _customWIPLevelsPath;
        set
        {
            if (SetProperty(ref _customWIPLevelsPath, value))
                SaveSettings();
        }
    }

    public string OriginalScriptPath1
    {
        get => _originalScriptPath1;
        set
        {
            if (SetProperty(ref _originalScriptPath1, value))
                SaveSettings();
        }
    }

    public string OriginalScriptPath2
    {
        get => _originalScriptPath2;
        set
        {
            if (SetProperty(ref _originalScriptPath2, value))
                SaveSettings();
        }
    }

    public string OriginalScriptPath3
    {
        get => _originalScriptPath3;
        set
        {
            if (SetProperty(ref _originalScriptPath3, value))
                SaveSettings();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand AddMetadataCommand { get; }
    public AsyncRelayCommand ExportZipCommand { get; }
    public AsyncRelayCommand FindOriginalScriptsCommand { get; }
    public RelayCommand BrowseCustomLevelsCommand { get; }
    public RelayCommand BrowseCustomWIPLevelsCommand { get; }
    public RelayCommand BrowseOriginalScript1Command { get; }
    public RelayCommand BrowseOriginalScript2Command { get; }
    public RelayCommand BrowseOriginalScript3Command { get; }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        _customLevelsPath = settings.CustomLevelsPath;
        _customWIPLevelsPath = settings.CustomWIPLevelsPath;
        _originalScriptPath1 = settings.OriginalScriptPath1;
        _originalScriptPath2 = settings.OriginalScriptPath2;
        _originalScriptPath3 = settings.OriginalScriptPath3;
        OnPropertyChanged(nameof(CustomLevelsPath));
        OnPropertyChanged(nameof(CustomWIPLevelsPath));
        OnPropertyChanged(nameof(OriginalScriptPath1));
        OnPropertyChanged(nameof(OriginalScriptPath2));
        OnPropertyChanged(nameof(OriginalScriptPath3));
    }

    private void SaveSettings()
    {
        _settingsService.Save(new AppSettings
        {
            CustomLevelsPath = _customLevelsPath,
            CustomWIPLevelsPath = _customWIPLevelsPath,
            OriginalScriptPath1 = _originalScriptPath1,
            OriginalScriptPath2 = _originalScriptPath2,
            OriginalScriptPath3 = _originalScriptPath3
        });
    }

    private Task ScanAsync()
    {
        StatusText = "スキャン中...";

        var entries = new System.Collections.Generic.List<CameraScriptManager.Models.CameraScriptEntry>();

        var dialog = new CameraScriptManager.Views.ProgressDialog("カメラスクリプトを読込中...", async () =>
        {
            entries = await Task.Run(() => _scanner.Scan(CustomLevelsPath, CustomWIPLevelsPath));
        });

        if (Application.Current.MainWindow != null)
        {
            dialog.Owner = Application.Current.MainWindow;
        }
        dialog.ShowDialog();

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

        var response = await _apiClient.GetMapAsync(item.MapId);
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
            return Task.CompletedTask;
        }

        var result = MessageBox.Show(
            $"選択された {targetItems.Count} 件のカメラスクリプトにメタデータを追加します。\n変更前のファイルはバックアップされます。\nよろしいですか？",
            "メタ情報追加",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return Task.CompletedTask;

        StatusText = "メタ情報追加中...";

        var files = new List<(string fullFilePath, string originalJson, string newJson, string sourceType)>();

        foreach (var item in targetItems)
        {
            var newJson = MetadataService.PrepareJsonWithMetadata(
                item.Entry.JsonContent,
                item.MapId,
                item.CameraScriptAuthorName,
                item.Bpm,
                item.Duration,
                item.SongName,
                item.SongSubName,
                item.SongAuthorName,
                item.LevelAuthorName);

            files.Add((item.FullFilePath, item.Entry.JsonContent, newJson, item.SourceType));
        }

        try
        {
            MetadataService.CreateBackupAndWriteMetadata(files, CustomLevelsPath, CustomWIPLevelsPath);

            // Update internal state after writing
            foreach (var item in targetItems)
            {
                item.Entry.JsonContent = File.ReadAllText(item.FullFilePath);
                item.Entry.HasOriginalMetadata = true;
                item.IsModified = false;
            }

            StatusText = $"メタ情報追加完了: {targetItems.Count} 件のファイルを更新しました";
        }
        catch (Exception ex)
        {
            StatusText = $"エラー: {ex.Message}";
            MessageBox.Show($"メタ情報の追加中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    private Task ExportZipAsync()
    {
        var selectedItems = Items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            StatusText = "エクスポートする項目を選択してください";
            return Task.CompletedTask;
        }

        bool isSingle = selectedItems.Count == 1;
        string defaultFileName;

        if (isSingle)
        {
            var item = selectedItems[0];
            defaultFileName = ZipExportService.SanitizeFileName(
                $"{item.MapId}_{item.SongName}_{item.LevelAuthorName}.zip");
        }
        else
        {
            defaultFileName = "CameraScripts.zip";
        }

        var dialog = new SaveFileDialog
        {
            Filter = "ZIPファイル (*.zip)|*.zip",
            FileName = defaultFileName,
            Title = "カメラスクリプトのエクスポート"
        };

        if (dialog.ShowDialog() != true)
            return Task.CompletedTask;

        StatusText = "ZIPエクスポート中...";

        var items = new List<(string zipEntryFolder, string fileName, string jsonContent)>();

        foreach (var selected in selectedItems)
        {
            string folderName;
            if (isSingle)
            {
                folderName = "";
            }
            else
            {
                folderName = ZipExportService.SanitizeFileName(
                    $"{selected.MapId}_{selected.SongName}_{selected.LevelAuthorName}");
            }

            string content = selected.IsModified
                ? selected.GetCurrentJsonContent()
                : selected.Entry.JsonContent;

            items.Add((folderName, selected.FileName, content));
        }

        try
        {
            ZipExportService.Export(items, dialog.FileName);
            StatusText = $"ZIPエクスポート完了: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"エラー: {ex.Message}";
            MessageBox.Show($"ZIPエクスポート中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    private Task FindOriginalScriptsAsync()
    {
        var targetItems = Items.Where(i => i.IsSelected).ToList();
        if (targetItems.Count == 0)
        {
            StatusText = "元データ照合を行う項目を選択してください";
            return Task.CompletedTask;
        }

        var searchPaths = new List<string>
        {
            OriginalScriptPath1,
            OriginalScriptPath2,
            OriginalScriptPath3
        };

        var dialog = new CameraScriptManager.Views.ProgressDialog("元データを検索中...", async () =>
        {
            var service = new CameraScriptManager.Services.OriginalScriptMatchService(searchPaths, msg =>
            {
                Application.Current.Dispatcher.Invoke(() => StatusText = msg);
            });
            var entries = targetItems.Select(i => i.Entry).ToList();
            await service.MatchOriginalScriptsAsync(entries);
        });

        if (Application.Current.MainWindow != null)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        dialog.ShowDialog();

        // Update view models with matching results
        int matchCount = 0;
        foreach (var item in targetItems)
        {
            if (!string.IsNullOrEmpty(item.Entry.OriginalSourceFile))
            {
                item.OriginalSourceFile = item.Entry.OriginalSourceFile;
                matchCount++;
            }
        }

        StatusText = $"元データ照合完了: {matchCount}/{targetItems.Count} 件のファイルがマッチしました";
        return Task.CompletedTask;
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
