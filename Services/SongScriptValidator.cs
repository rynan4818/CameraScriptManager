using System.Text.Json;

namespace CameraScriptManager.Services;

public static class SongScriptValidator
{
    public static bool IsValidSongScript(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("Movements", out var movements)
                && movements.ValueKind == JsonValueKind.Array
                && movements.GetArrayLength() > 0)
            {
                return true;
            }
        }
        catch
        {
            // Not valid JSON
        }
        return false;
    }
}
