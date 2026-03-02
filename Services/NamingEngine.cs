using System.IO;
using System.Collections.Generic;

namespace CameraScriptManager.Services;

public static class NamingEngine
{
    public static string ReplaceTags(string format, Dictionary<string, string> tags)
    {
        string result = format;
        foreach (var tag in tags)
        {
            result = result.Replace($"{{{tag.Key}}}", tag.Value ?? "");
        }
        return SanitizeFileName(result);
    }

    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unnamed";
        
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}
