using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Linq;

namespace CameraScriptManager.Services;

public static class HashCalculator
{
    /// <summary>
    /// SongCore互換のSHA1ハッシュを計算します。
    /// 指定されたinfo.datとそれに付随する各.datファイルのバイト列を結合してハッシュ化します。
    /// </summary>
    public static string CalculateSongHash(string folderPath, IEnumerable<string> beatmapFilenames)
    {
        var infoDatPath = Path.Combine(folderPath, "info.dat");
        if (!File.Exists(infoDatPath))
            infoDatPath = Path.Combine(folderPath, "Info.dat");

        if (!File.Exists(infoDatPath))
            return string.Empty;

        try
        {
            IEnumerable<byte> combinedBytes = File.ReadAllBytes(infoDatPath);

            foreach (var filename in beatmapFilenames)
            {
                if (string.IsNullOrWhiteSpace(filename)) continue;

                var beatmapPath = Path.Combine(folderPath, filename);
                if (File.Exists(beatmapPath))
                {
                    combinedBytes = combinedBytes.Concat(File.ReadAllBytes(beatmapPath));
                }
            }

            return CreateSha1FromBytes(combinedBytes.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    // SongCoreのByteToHexBitFiddleブラックマジック実装
    private static string ByteToHexBitFiddle(byte[] bytes)
    {
        char[] c = new char[bytes.Length * 2];
        int b;
        for (int i = 0; i < bytes.Length; i++)
        {
            b = bytes[i] >> 4;
            c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
            b = bytes[i] & 0xF;
            c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
        }
        return new string(c);
    }

    private static string CreateSha1FromBytes(byte[] input)
    {
        using var sha1 = SHA1.Create();
        var hashBytes = sha1.ComputeHash(input);
        return ByteToHexBitFiddle(hashBytes);
    }
}
