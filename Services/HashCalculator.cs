using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Linq;

namespace CameraScriptManager.Services;

public static class HashCalculator
{
    /// <summary>
    /// SongCore互換のSHA1ハッシュを計算します。
    /// 指定されたInfo.datの生文字列と、SongCoreが対象にする追加ファイルを同順でハッシュ化します。
    /// </summary>
    public static string CalculateSongHash(string folderPath, string infoDatContent, IEnumerable<string> hashInputFileNames)
    {
        if (string.IsNullOrWhiteSpace(infoDatContent))
            return string.Empty;

        try
        {
            using var sha1 = SHA1.Create();
            byte[] prependBytes = Encoding.UTF8.GetBytes(infoDatContent);
            if (prependBytes.Length > 0)
            {
                sha1.TransformBlock(prependBytes, 0, prependBytes.Length, null, 0);
            }

            foreach (var filename in hashInputFileNames)
            {
                if (string.IsNullOrWhiteSpace(filename)) continue;

                var beatmapPath = Path.Combine(folderPath, filename);
                if (File.Exists(beatmapPath))
                {
                    byte[] bytes = File.ReadAllBytes(beatmapPath);
                    if (bytes.Length > 0)
                    {
                        sha1.TransformBlock(bytes, 0, bytes.Length, null, 0);
                    }
                }
            }

            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return ByteToHexBitFiddle(sha1.Hash ?? Array.Empty<byte>());
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string CalculateSongHash(string folderPath, InfoDatData infoDatData)
    {
        if (infoDatData == null)
            return string.Empty;

        return CalculateSongHash(folderPath, infoDatData.RawInfoDatContent, infoDatData.BeatmapFilenames);
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

}
