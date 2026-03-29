using System.IO;

namespace CameraScriptManager.Services;

public static class SongScriptsPathResolver
{
    public static string ResolveSongScriptsFolderPath(AppSettings settings)
    {
        var configuredPath = settings.SongScriptsFolderPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        return ResolveConfiguredPath(configuredPath, GetGameRootPath(settings));
    }

    public static string GetRelativePathUnderSongScripts(string songScriptsRootPath, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(songScriptsRootPath) || string.IsNullOrWhiteSpace(sourceFilePath))
            return Path.GetFileName(sourceFilePath ?? string.Empty);

        try
        {
            string fullRoot = NormalizePath(songScriptsRootPath);
            string fullSource = NormalizePath(sourceFilePath);

            if (!fullSource.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(fullSource);

            return Path.GetRelativePath(fullRoot, fullSource);
        }
        catch
        {
            return Path.GetFileName(sourceFilePath);
        }
    }

    private static string ResolveConfiguredPath(string configuredPath, string baseRootPath)
    {
        string candidatePath = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        if (!Path.IsPathRooted(candidatePath))
        {
            candidatePath = Path.Combine(baseRootPath, candidatePath);
        }

        return NormalizePath(candidatePath);
    }

    private static string GetGameRootPath(AppSettings settings)
    {
        if (TryResolveGameRoot(settings.CustomLevelsPath, out string customLevelsRoot))
            return customLevelsRoot;

        if (TryResolveGameRoot(settings.CustomWIPLevelsPath, out string customWipRoot))
            return customWipRoot;

        return NormalizePath(AppDomain.CurrentDomain.BaseDirectory);
    }

    private static bool TryResolveGameRoot(string configuredPath, out string gameRootPath)
    {
        gameRootPath = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPath))
            return false;

        try
        {
            string fullPath = NormalizePath(Environment.ExpandEnvironmentVariables(configuredPath));
            if (string.IsNullOrWhiteSpace(fullPath))
                return false;

            var directory = new DirectoryInfo(fullPath);
            while (directory != null)
            {
                if (string.Equals(directory.Name, "Beat Saber_Data", StringComparison.OrdinalIgnoreCase))
                {
                    if (directory.Parent == null)
                        return false;

                    gameRootPath = NormalizePath(directory.Parent.FullName);
                    return true;
                }

                directory = directory.Parent;
            }
        }
        catch
        {
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
