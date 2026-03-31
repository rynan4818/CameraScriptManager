using System.Diagnostics;
using System.IO;

namespace CameraScriptManager.Services;

public static class DebugLogFileWriter
{
    private static readonly object SyncRoot = new();

    [Conditional("DEBUG")]
    public static void WriteLine(string fileName, string category, string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{category}] {message}";
        Debug.WriteLine(line);

        try
        {
            lock (SyncRoot)
            {
                string logPath = AppRuntimePaths.GetDebugLogFilePath(fileName);
                AppRuntimePaths.EnsureParentDirectory(logPath);
                File.AppendAllText(logPath, line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }
}
