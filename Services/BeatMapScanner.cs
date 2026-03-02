using System.IO;
using CameraScriptManager.Models;

namespace CameraScriptManager.Services;

public class BeatMapScanner
{
    public Dictionary<string, List<BeatMapFolder>> ScanFolder(string rootPath, bool isCustomLevels)
    {
        var result = new Dictionary<string, List<BeatMapFolder>>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(rootPath))
            return result;

        foreach (var dir in Directory.GetDirectories(rootPath))
        {
            string folderName = Path.GetFileName(dir);
            string? hexId = HexIdExtractor.ExtractHexId(folderName);
            if (hexId == null) continue;

            var folder = new BeatMapFolder
            {
                HexId = hexId,
                FolderName = folderName,
                FullPath = dir,
                IsCustomLevels = isCustomLevels
            };

            if (!result.TryGetValue(hexId, out var list))
            {
                list = new List<BeatMapFolder>();
                result[hexId] = list;
            }
            list.Add(folder);
        }

        return result;
    }
}
