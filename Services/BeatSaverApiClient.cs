using System.Net.Http;
using System.Text.Json;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public class BeatSaverApiClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly Dictionary<string, BeatSaverApiResponse?> _cache = new();
    private readonly SongDetailsCacheService _cacheService;

#if DEBUG
    private static void DebugLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [ApiClient] {message}";
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

    public BeatSaverApiClient(SongDetailsCacheService cacheService)
    {
        _cacheService = cacheService;
        _client = new HttpClient();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("CameraScriptManager/1.0");
        _client.BaseAddress = new Uri("https://api.beatsaver.com/");
        _client.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<(BeatSaverApiResponse? Response, bool FromApi, bool? CacheHit)> GetMapAsync(string hexId)
    {
        string key = hexId.ToLowerInvariant();
        if (_cache.TryGetValue(key, out var cached))
        {
#if DEBUG
            DebugLog($"GetMapAsync(\"{key}\"): found in in-memory cache");
#endif
            return (cached, false, null);
        }

        // SongDetailsCacheから優先取得
#if DEBUG
        DebugLog($"GetMapAsync(\"{key}\"): trying SongDetailsCache...");
#endif
        if (_cacheService.TryGetByMapId(key, out var cacheResponse))
        {
#if DEBUG
            DebugLog($"GetMapAsync(\"{key}\"): SongDetailsCache HIT");
#endif
            _cache[key] = cacheResponse;
            return (cacheResponse, false, true);
        }

        // キャッシュにない場合はBeatSaver APIにフォールバック
#if DEBUG
        DebugLog($"GetMapAsync(\"{key}\"): SongDetailsCache MISS, calling BeatSaver API...");
#endif
        try
        {
            var response = await _client.GetAsync($"maps/id/{key}");
#if DEBUG
            DebugLog($"GetMapAsync(\"{key}\"): API response status={response.StatusCode}");
#endif
            if (!response.IsSuccessStatusCode)
            {
                _cache[key] = null;
                return (null, true, false);
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<BeatSaverApiResponse>(json);
            _cache[key] = result;
            return (result, true, false);
        }
        catch (Exception ex)
        {
#if DEBUG
            DebugLog($"GetMapAsync(\"{key}\"): API EXCEPTION: {ex.GetType().Name}: {ex.Message}");
#endif
            return (null, true, false);
        }
    }

    public async Task<BeatSaverApiResponse?> GetMapFromApiAsync(string hexId)
    {
        string key = hexId.ToLowerInvariant();
        try
        {
            var response = await _client.GetAsync($"maps/id/{key}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<BeatSaverApiResponse>(json);
            if (result != null)
            {
                _cache[key] = result;
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    public Task<byte[]> DownloadBytesAsync(string url)
    {
        return _client.GetByteArrayAsync(url);
    }

    public void Dispose() => _client?.Dispose();
}
