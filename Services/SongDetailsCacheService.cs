using CameraScriptManager.Models;
using SongDetailsCache;
using SongDetailsCache.Structs;

namespace CameraScriptManager.Services;

public class SongDetailsCacheService
{
    private SongDetails? _details;
    private readonly object _lock = new();
    private Task? _initTask;

#if DEBUG
    private static void DebugLog(string message)
    {
        DebugLogFileWriter.WriteLine("debug_songdetails.log", "CacheService", message);
    }
#endif

    public bool IsAvailable
    {
        get
        {
            lock (_lock)
                return _details != null;
        }
    }

    public Task InitAsync()
    {
        _initTask = InitCoreAsync();
        return _initTask;
    }

    /// <summary>
    /// 初期化が完了するまで待機します。初期化失敗時も例外をスローせず正常に完了します。
    /// </summary>
    public Task EnsureInitializedAsync()
    {
        return _initTask ?? Task.CompletedTask;
    }

    private async Task InitCoreAsync()
    {
        try
        {
#if DEBUG
            DebugLog("InitCoreAsync: START");
#endif
            var cacheDir = AppRuntimePaths.UserDataDirectory;

            SongDetails.SetCacheDirectory(cacheDir);
#if DEBUG
            DebugLog($"InitCoreAsync: cacheDir={cacheDir}, calling SongDetails.Init()...");
#endif
            var details = await SongDetails.Init();

            lock (_lock)
            {
                _details = details;
            }
#if DEBUG
            DebugLog($"InitCoreAsync: SUCCESS. _details set. songs.Length={details?.songs?.Length}");
#endif
        }
 #if DEBUG
        catch (Exception ex)
        {
            // TypeInitializationExceptionの場合、InnerExceptionに真の原因がある
            var innerMsg = ex.InnerException != null
                ? $"\n  InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}"
                : "";
            DebugLog($"InitCoreAsync: EXCEPTION: {ex.GetType().Name}: {ex.Message}{innerMsg}\n{ex.StackTrace}");
            // 初期化失敗時はAPIフォールバックに任せる
        }
#else
        catch (Exception)
        {
            // 初期化失敗時はAPIフォールバックに任せる
        }
#endif
    }

    public bool TryGetByMapId(string hexId, out BeatSaverApiResponse response)
    {
        response = null!;

        SongDetails? details;
        lock (_lock)
            details = _details;

        if (details == null)
        {
#if DEBUG
            DebugLog($"TryGetByMapId(\"{hexId}\"): _details is NULL (cache not initialized)");
#endif
            return false;
        }

        try
        {
#if DEBUG
            DebugLog($"TryGetByMapId(\"{hexId}\"): _details available, calling FindByMapId...");
#endif
            if (details.songs.FindByMapId(hexId, out Song song))
            {
#if DEBUG
                DebugLog($"TryGetByMapId(\"{hexId}\"): HIT - songName=\"{song.songName}\", mapId=0x{song.mapId:X}");
#endif
                response = ConvertToResponse(song);
                return true;
            }
#if DEBUG
            DebugLog($"TryGetByMapId(\"{hexId}\"): MISS - not found in cache");
#endif
        }
 #if DEBUG
        catch (Exception ex)
        {
            DebugLog($"TryGetByMapId(\"{hexId}\"): EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
#else
        catch (Exception)
        {
        }
#endif

        return false;
    }

    public bool TryGetByHash(string hash, out BeatSaverApiResponse response)
    {
        response = null!;

        SongDetails? details;
        lock (_lock)
            details = _details;

        if (details == null)
        {
#if DEBUG
            DebugLog($"TryGetByHash(\"{hash}\"): _details is NULL");
#endif
            return false;
        }

        try
        {
            if (details.songs.FindByHash(hash, out Song song))
            {
#if DEBUG
                DebugLog($"TryGetByHash(\"{hash}\"): HIT - songName=\"{song.songName}\"");
#endif
                response = ConvertToResponse(song);
                return true;
            }
#if DEBUG
            DebugLog($"TryGetByHash(\"{hash}\"): MISS");
#endif
        }
 #if DEBUG
        catch (Exception ex)
        {
            DebugLog($"TryGetByHash(\"{hash}\"): EXCEPTION: {ex.GetType().Name}: {ex.Message}");
        }
#else
        catch (Exception)
        {
        }
#endif

        return false;
    }

    private static BeatSaverApiResponse ConvertToResponse(Song song)
    {
        return new BeatSaverApiResponse
        {
            Id = song.key,
            Name = song.songName,
            Metadata = new BeatSaverMetadata
            {
                Bpm = song.bpm,
                Duration = (int)song.songDurationSeconds,
                SongName = song.songName,
                SongSubName = "",
                SongAuthorName = song.songAuthorName,
                LevelAuthorName = song.levelAuthorName
            }
        };
    }
}
