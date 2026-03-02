namespace CameraScriptManager.Models;

public class BeatMapFolder
{
    public string HexId { get; set; } = "";
    public string FolderName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsCustomLevels { get; set; }

    public override string ToString() => FolderName;
}
