using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using NVorbis;

namespace CameraScriptManager.Services;

public class OggDurationService
{
    private readonly ConcurrentDictionary<string, double> _cache = new();

    public double GetDurationFromFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return 0;

        if (_cache.TryGetValue(folderPath, out var duration))
            return duration;

        try
        {
            var files = Directory.GetFiles(folderPath, "*.egg")
                .Concat(Directory.GetFiles(folderPath, "*.ogg"))
                .ToArray();

            if (files.Length > 0)
            {
                using var vorbis = new VorbisReader(files[0]);
                duration = vorbis.TotalTime.TotalSeconds;
            }
        }
        catch
        {
            duration = 0;
        }

        _cache[folderPath] = duration;
        return duration;
    }
}
