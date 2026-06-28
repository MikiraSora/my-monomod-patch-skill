namespace MonoModDiffClassifier.Tests;

/// <summary>
/// 把 base/head 文本行级 diff 成 git diff 文本格式，喂给 Classifier。
/// 仅用于测试夹具自包含（不依赖真实 git）。
/// </summary>
internal static class DiffBuilder
{
    public static string Build(string fileName, string baseSrc, string headSrc)
    {
        var baseLines = SplitLines(baseSrc);
        var headLines = SplitLines(headSrc);
        var hunks = LcsDiff(baseLines, headLines);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"diff --git a/{fileName} b/{fileName}");
        sb.AppendLine($"--- a/{fileName}");
        sb.AppendLine($"+++ b/{fileName}");

        foreach (var (type, line) in hunks)
        {
            sb.AppendLine(type switch
            {
                LineType.Add => "+" + line,
                LineType.Del => "-" + line,
                _            => " " + line,
            });
        }
        return sb.ToString();
    }

    private static List<string> SplitLines(string src)
    {
        if (string.IsNullOrEmpty(src)) return new List<string>();
        var lines = src.Replace("\r\n", "\n").Split('\n', StringSplitOptions.None).ToList();
        // 去掉末尾因末尾换行产生的空串。
        if (lines.Count > 0 && lines[^1] == "") lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    private enum LineType { Same, Add, Del }

    private static List<(LineType, string)> LcsDiff(List<string> a, List<string> b)
    {
        var n = a.Count; var m = b.Count;
        // dp[i][j] = a 前 i 行与 b 前 j 行的 LCS 长度。
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var result = new List<(LineType, string)>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y]) { result.Add((LineType.Same, a[x])); x++; y++; }
            else if (dp[x + 1, y] >= dp[x, y + 1]) { result.Add((LineType.Del, a[x])); x++; }
            else { result.Add((LineType.Add, b[y])); y++; }
        }
        while (x < n) { result.Add((LineType.Del, a[x])); x++; }
        while (y < m) { result.Add((LineType.Add, b[y])); y++; }
        return result;
    }
}
