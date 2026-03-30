using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace CameraScriptManager.Services;

public sealed class AppUpdateCheckService
{
    public const string ReleaseInfoUrl = "https://rynan4818.github.io/release_info.json";
    public const string ReleasePageUrl = "https://github.com/rynan4818/CameraScriptManager/releases";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);
    private readonly SettingsService _settingsService = new();

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        AppSettings settings = _settingsService.Load();
        string currentVersion = GetCurrentVersionString();
        string latestVersion = settings.LastKnownLatestCameraScriptManagerVersion ?? string.Empty;
        bool checkedOnlineSuccessfully = false;

        if (!settings.EnableAutoUpdateCheck)
        {
            return new AppUpdateCheckResult
            {
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                ReleaseUrl = ReleasePageUrl,
                IsUpdateAvailable = false,
                WasCheckedOnline = false
            };
        }

        bool shouldCheckOnline = !settings.LastUpdateCheckUtc.HasValue ||
            DateTime.UtcNow - settings.LastUpdateCheckUtc.Value >= CheckInterval;

        if (shouldCheckOnline)
        {
            settings.LastUpdateCheckUtc = DateTime.UtcNow;

            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };

                using HttpResponseMessage response = await httpClient.GetAsync(ReleaseInfoUrl, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                string? fetchedVersion = TryReadLatestCameraScriptManagerVersion(json);
                if (!string.IsNullOrWhiteSpace(fetchedVersion))
                {
                    latestVersion = fetchedVersion.Trim();
                    settings.LastKnownLatestCameraScriptManagerVersion = latestVersion;
                    checkedOnlineSuccessfully = true;
                }
            }
            catch
            {
                // 起動を止めないことを優先し、更新チェック失敗は無視する
            }

            _settingsService.Save(settings);
        }

        return CreateResult(currentVersion, latestVersion, checkedOnlineSuccessfully);
    }

    public static string GetCurrentVersionString()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            string normalizedInformationalVersion = informationalVersion.Split('+')[0].Trim();
            if (TryParseVersion(normalizedInformationalVersion, out Version? parsedInformationalVersion) &&
                parsedInformationalVersion is not null)
            {
                return FormatVersion(parsedInformationalVersion);
            }
        }

        Version? assemblyVersion = assembly.GetName().Version;
        return assemblyVersion != null ? FormatVersion(assemblyVersion) : "0.0.0";
    }

    public static bool IsUpdateAvailable(string currentVersion, string latestVersion)
    {
        return TryParseVersion(currentVersion, out Version? current) &&
            current is not null &&
            TryParseVersion(latestVersion, out Version? latest) &&
            latest is not null &&
            latest > current;
    }

    private static AppUpdateCheckResult CreateResult(string currentVersion, string latestVersion, bool wasCheckedOnline)
    {
        return new AppUpdateCheckResult
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            ReleaseUrl = ReleasePageUrl,
            IsUpdateAvailable = IsUpdateAvailable(currentVersion, latestVersion),
            WasCheckedOnline = wasCheckedOnline
        };
    }

    private static string? TryReadLatestCameraScriptManagerVersion(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("LatestVersion", out JsonElement latestVersionElement))
        {
            return null;
        }

        if (!latestVersionElement.TryGetProperty("CameraScriptManager", out JsonElement cameraScriptManagerElement))
        {
            return null;
        }

        return cameraScriptManagerElement.GetString();
    }

    private static bool TryParseVersion(string? versionText, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return false;
        }

        return Version.TryParse(versionText.Trim(), out version);
    }

    private static string FormatVersion(Version version)
    {
        if (version.Revision > 0)
        {
            return version.ToString(4);
        }

        if (version.Build > 0)
        {
            return version.ToString(3);
        }

        return version.ToString(2);
    }
}

public sealed class AppUpdateCheckResult
{
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string ReleaseUrl { get; init; } = AppUpdateCheckService.ReleasePageUrl;
    public bool IsUpdateAvailable { get; init; }
    public bool WasCheckedOnline { get; init; }
}
