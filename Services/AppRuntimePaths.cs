using System.IO;

namespace CameraScriptManager.Services;

public static class AppRuntimePaths
{
    private static readonly object MigrationSyncRoot = new();

    public static string BaseDirectory => NormalizePath(AppDomain.CurrentDomain.BaseDirectory);

    public static string UserDataDirectory => EnsureDirectory(Path.Combine(BaseDirectory, "UserData"));

    public static string GetSettingsFilePath()
    {
        return GetManagedFilePath("settings.json");
    }

    public static string GetSearchCacheFilePath()
    {
        return GetManagedFilePath("CameraScriptManager.SearchCache.json");
    }

    public static string GetDefaultBackupRootPath()
    {
        return EnsureDirectory(Path.Combine(UserDataDirectory, "backup"));
    }

    public static string GetDebugLogFilePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A log file name is required.", nameof(fileName));
        }

        string logPath = Path.Combine(UserDataDirectory, fileName.Trim());
        EnsureParentDirectory(logPath);
        return logPath;
    }

    public static void EnsureParentDirectory(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        string? directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    private static string GetManagedFilePath(string fileName)
    {
        string targetPath = Path.Combine(UserDataDirectory, fileName);
        EnsureParentDirectory(targetPath);

        string legacyPath = Path.Combine(BaseDirectory, fileName);
        lock (MigrationSyncRoot)
        {
            if (!File.Exists(targetPath) && File.Exists(legacyPath))
            {
                try
                {
                    File.Copy(legacyPath, targetPath);
                }
                catch
                {
                }
            }
        }

        return targetPath;
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return NormalizePath(path);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

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
