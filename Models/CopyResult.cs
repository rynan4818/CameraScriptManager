namespace CameraScriptManager.Models;

public class CopyResult
{
    public string HexId { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool WasOverwrite { get; set; }
}
