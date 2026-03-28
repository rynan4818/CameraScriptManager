namespace CameraScriptManager.Models;

public class SongScriptsSaveResult
{
    public string SourceFilePath { get; set; } = "";
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
    public List<SongScriptsManagerEntry> Entries { get; set; } = new();
}
