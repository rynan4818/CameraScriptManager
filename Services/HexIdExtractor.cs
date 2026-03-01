using System.Text.RegularExpressions;

namespace CameraScriptManager.Services;

public static partial class HexIdExtractor
{
    [GeneratedRegex(@"^([0-9a-fA-F]{1,6})[\s\(]", RegexOptions.Compiled)]
    private static partial Regex HexIdPattern();

    [GeneratedRegex(@"(?:[\s_\-\]])([0-9a-fA-F]{1,6})[\s\(]", RegexOptions.Compiled)]
    private static partial Regex HexIdFallbackPattern();

    [GeneratedRegex(@"[0-9a-fA-F]+", RegexOptions.Compiled)]
    private static partial Regex RawHexPattern();

    public static string? ExtractHexId(string nameOrPath)
    {
        var match = HexIdPattern().Match(nameOrPath);
        if (match.Success)
            return match.Groups[1].Value.ToLowerInvariant();

        var fallback = HexIdFallbackPattern().Match(nameOrPath);
        if (fallback.Success)
            return fallback.Groups[1].Value.ToLowerInvariant();

        return FindBestHexCandidate(nameOrPath);
    }

    private static string? FindBestHexCandidate(string input)
    {
        var matches = RawHexPattern().Matches(input);
        if (matches.Count == 0)
            return null;

        Match? best = null;
        int bestScore = int.MaxValue;

        foreach (Match m in matches)
        {
            int len = m.Value.Length;
            if (len < 2) continue;

            int score;
            if (len >= 4 && len <= 6)
                score = 0;
            else if (len < 4)
                score = 4 - len;
            else
                score = len - 6;

            bool hasHexChar = m.Value.Any(c => (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
            if (!hasHexChar)
                score += 100;

            if (score < bestScore)
            {
                bestScore = score;
                best = m;
            }
        }

        return best?.Value.ToLowerInvariant();
    }
}
