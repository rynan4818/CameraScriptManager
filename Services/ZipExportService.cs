using System.IO;
using System.IO.Compression;
using System.Text;

namespace CameraScriptManager.Services;

public static class ZipExportService
{
    public static void Export(
        IList<(string zipEntryFolder, string fileName, string jsonContent)> items,
        string zipFilePath)
    {
        using var zipStream = File.Create(zipFilePath);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        foreach (var (folder, fileName, content) in items)
        {
            string entryPath;
            if (string.IsNullOrWhiteSpace(folder))
                entryPath = fileName;
            else
                entryPath = $"{folder}/{fileName}";

            var entry = archive.CreateEntry(entryPath);
            using var entryStream = entry.Open();
            using var sw = new StreamWriter(entryStream, Encoding.UTF8);
            sw.Write(content);
        }
    }

    public static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
