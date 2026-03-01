using System.Net.Http;
using System.Text.Json;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public class BeatSaverApiClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly Dictionary<string, BeatSaverApiResponse?> _cache = new();

    public BeatSaverApiClient()
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("CameraScriptManager/1.0");
        _client.BaseAddress = new Uri("https://api.beatsaver.com/");
        _client.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<BeatSaverApiResponse?> GetMapAsync(string hexId)
    {
        string key = hexId.ToLowerInvariant();
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var response = await _client.GetAsync($"maps/id/{key}");
            if (!response.IsSuccessStatusCode)
            {
                _cache[key] = null;
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<BeatSaverApiResponse>(json);
            _cache[key] = result;
            return result;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _client?.Dispose();
}
