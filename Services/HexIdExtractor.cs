using System.Text.RegularExpressions;

namespace CameraScriptManager.Services;

public static partial class HexIdExtractor
{
    /// <summary>先頭から始まるHex ID（1〜6桁）+ スペースまたは括弧</summary>
    [GeneratedRegex(@"^([0-9a-fA-F]{1,6})[\s\(]", RegexOptions.Compiled)]
    private static partial Regex HexIdPattern();

    /// <summary>先頭以外の位置にあるHex ID（1〜6桁）+ スペースまたは括弧</summary>
    [GeneratedRegex(@"(?:[\s_\-\]])([0-9a-fA-F]{1,6})[\s\(]", RegexOptions.Compiled)]
    private static partial Regex HexIdFallbackPattern();

    /// <summary>連続する16進数文字列を単純検索するパターン</summary>
    [GeneratedRegex(@"[0-9a-fA-F]+", RegexOptions.Compiled)]
    private static partial Regex RawHexPattern();

    /// <summary>
    /// 文字列からHex IDを抽出する。
    /// まず先頭からの一致を試み、見つからない場合は先頭以外も検索する。
    /// </summary>
    public static string? ExtractHexId(string nameOrPath)
    {
        // 先頭優先
        var match = HexIdPattern().Match(nameOrPath);
        if (match.Success)
            return match.Groups[1].Value.ToLowerInvariant();

        // フォールバック: 先頭以外の位置も検索
        var fallback = HexIdFallbackPattern().Match(nameOrPath);
        if (fallback.Success)
            return fallback.Groups[1].Value.ToLowerInvariant();

        // 最終フォールバック: 単純に16進数文字列を検索し、4〜6桁に最も近いものを選択
        return FindBestHexCandidate(nameOrPath);
    }

    public static string ExtractSongName(string name)
    {
        var match = HexIdPattern().Match(name);
        if (!match.Success)
        {
            // フォールバックでマッチした場合も曲名抽出を試みる
            var fallback = HexIdFallbackPattern().Match(name);
            if (fallback.Success)
            {
                // フォールバック位置以降を曲名として扱う
                string after = name[(fallback.Index + fallback.Length)..].Trim();
                if (after.StartsWith('(') && after.Contains(')'))
                {
                    int closeIdx = after.LastIndexOf(')');
                    after = after[1..closeIdx];
                }
                return after.Trim();
            }

            // 最終フォールバック: RawHexPatternで見つかったIDの前後から曲名を推測
            var rawMatch = FindBestHexMatch(name);
            if (rawMatch == null)
                return name;
            // マッチ部分を除去し残りを曲名とする
            string remaining = (name[..rawMatch.Index] + name[(rawMatch.Index + rawMatch.Length)..]).Trim();
            remaining = remaining.Trim(' ', '-', '_', '(', ')');
            return string.IsNullOrWhiteSpace(remaining) ? name : remaining;
        }

        string remainder = name[match.Length..].Trim();
        // Remove surrounding parentheses
        if (remainder.StartsWith('(') && remainder.Contains(')'))
        {
            int closeIdx = remainder.LastIndexOf(')');
            remainder = remainder[1..closeIdx];
        }
        // Remove trailing suffixes like V2, org, etc. after closing paren
        return remainder.Trim();
    }

    /// <summary>
    /// 文字列中の16進数候補から4〜6桁に最も近いものを選択して返す。
    /// </summary>
    private static string? FindBestHexCandidate(string input)
    {
        var bestMatch = FindBestHexMatch(input);
        return bestMatch?.Value.ToLowerInvariant();
    }

    /// <summary>
    /// 文字列中の16進数候補から4〜6桁に最も近いMatchオブジェクトを返す。
    /// 純粋な10進数のみの候補は除外する（a-fを含むもの、または先頭が0のものを優先）。
    /// </summary>
    private static Match? FindBestHexMatch(string input)
    {
        var matches = RawHexPattern().Matches(input);
        if (matches.Count == 0)
            return null;

        Match? best = null;
        int bestScore = int.MaxValue;

        foreach (Match m in matches)
        {
            int len = m.Value.Length;
            if (len < 2) continue; // 1桁は除外

            // 4〜6桁からの距離をスコアとする（小さいほど良い）
            int score;
            if (len >= 4 && len <= 6)
                score = 0; // 理想的な桁数
            else if (len < 4)
                score = 4 - len;
            else
                score = len - 6;

            // a-fを含むものを優先（純粋な10進数より確実にHex ID）
            bool hasHexChar = m.Value.Any(c => (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
            if (!hasHexChar)
                score += 100; // 10進数のみの場合はペナルティ

            if (score < bestScore)
            {
                bestScore = score;
                best = m;
            }
        }

        return best;
    }
}
