using System.IO;

namespace CameraScriptManager.Services;

public static class BackupPathResolver
{
    public static string GetDefaultBackupRootPath()
    {
        return NormalizePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup"));
    }

    public static string ResolveBackupRootPath(AppSettings settings)
    {
        return ResolveBackupRootPath(settings.BackupRootPath);
    }

    public static string ResolveBackupRootPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return GetDefaultBackupRootPath();
        }

        string candidatePath = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        if (!Path.IsPathRooted(candidatePath))
        {
            candidatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, candidatePath);
        }

        return NormalizePath(candidatePath);
    }

    public static string GetMapScriptsBackupDirectory(string backupRootPath)
    {
        return Path.Combine(ResolveBackupRootPath(backupRootPath), "MapScripts");
    }

    public static string GetSongScriptsBackupDirectory(string backupRootPath)
    {
        return Path.Combine(ResolveBackupRootPath(backupRootPath), "SongScripts");
    }

    public static string GetCopierBackupDirectory(string backupRootPath)
    {
        return Path.Combine(ResolveBackupRootPath(backupRootPath), "Copier");
    }

    public static string AppendTimestampToFileName(string fileName, DateTime timestamp)
    {
        string extension = Path.GetExtension(fileName);
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string suffix = "_" + timestamp.ToString("yyyyMMdd_HHmmss");
        return string.IsNullOrEmpty(extension)
            ? baseName + suffix
            : baseName + suffix + extension;
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
