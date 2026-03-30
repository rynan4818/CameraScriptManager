using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public static class SongScriptsMetadataJsonService
{
    public static string PrepareJsonWithMetadata(SongScriptsManagerEntry entry, string originalJson)
    {
        return MetadataService.PrepareJsonWithMetadata(
            originalJson,
            entry.MapId ?? "",
            entry.Hash ?? "",
            entry.CameraScriptAuthorName ?? "",
            entry.Bpm,
            entry.Duration,
            entry.SongName ?? "",
            entry.SongSubName ?? "",
            entry.SongAuthorName ?? "",
            entry.LevelAuthorName ?? "",
            entry.AvatarHeight,
            entry.Description ?? "");
    }
}
