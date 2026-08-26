using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace FlexTimeChecker;

internal sealed record SearchResult
{
    public bool Found { get; init; }
    public string? FilePath { get; init; }
    public string? BindFilePath { get; init; }
    public DateTime? DispenseTime { get; init; }
    public string? HousingCode { get; init; }
    public string? MaterialCode { get; init; }
    public string? Error { get; init; }
    public int FilesScanned { get; init; }
    public long ElapsedMs { get; init; }
    public FolderProfile? Profile { get; init; }
}

internal static class DispenseSearch
{
    private static readonly Regex LabeledTime = new(
        @"(?:点胶(?:完成)?时间|点胶完成|Dispense(?:Time)?|GlueTime|完成时间)\s*[:：=]?\s*(\d{4}[-/]\d{2}[-/]\d{2}[ T]\d{2}:\d{2}:\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnyTime = new(
        @"(\d{4}[-/]\d{2}[-/]\d{2}[ T]\d{2}:\d{2}:\d{2})",
        RegexOptions.Compiled);

    private static readonly string[] SkipExt =
        [".exe", ".dll", ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".zip", ".7z", ".rar", ".pdf"];

    public static SearchResult Find(string housingCode, AppConfig cfg)
    {
        var sw = Stopwatch.StartNew();
        housingCode = (housingCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(housingCode))
            return new SearchResult { Error = "Housing 码为空", ElapsedMs = sw.ElapsedMilliseconds };

        if (!Directory.Exists(cfg.MessageFolder))
            return new SearchResult { Error = $"报文目录不存在：{cfg.MessageFolder}", ElapsedMs = sw.ElapsedMilliseconds };

        var extSet = new HashSet<string>(cfg.FileExtensions, StringComparer.OrdinalIgnoreCase);
        string[] files;
        try
        {
            files = Directory.GetFiles(cfg.MessageFolder, "*.*", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            return new SearchResult { Error = "列出文件失败：" + ex.Message, ElapsedMs = sw.ElapsedMilliseconds };
        }

        var candidates = new List<string>(files.Length);
        foreach (var path in files)
        {
            var ext = Path.GetExtension(path);
            if (SkipExt.Contains(ext, StringComparer.OrdinalIgnoreCase))
                continue;
            if (extSet.Count > 0 && !extSet.Contains(ext))
                continue;
            candidates.Add(path);
        }

        // 1) 文件名先命中（最快）
        foreach (var path in candidates)
        {
            if (Path.GetFileName(path).Contains(housingCode, StringComparison.OrdinalIgnoreCase))
            {
                var named = TryRead(path, housingCode, cfg.MaxReadBytes, requireCodeInContent: false);
                if (named.found)
                    return Finish(path, named.time, candidates.Count, sw);
            }
        }

        // 2) 内容并行搜索
        string? hitPath = null;
        DateTime? hitTime = null;
        var locker = new object();
        Parallel.ForEach(candidates, (path, state) =>
        {
            var read = TryRead(path, housingCode, cfg.MaxReadBytes, requireCodeInContent: true);
            if (!read.found)
                return;
            lock (locker)
            {
                if (hitPath != null)
                    return;
                hitPath = path;
                hitTime = read.time;
                state.Stop();
            }
        });

        if (hitPath != null)
            return Finish(hitPath, hitTime, candidates.Count, sw);

        return new SearchResult
        {
            Found = false,
            Error = $"未找到包含 {housingCode} 的报文",
            FilesScanned = candidates.Count,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    private static SearchResult Finish(string path, DateTime? time, int scanned, Stopwatch sw)
    {
        return new SearchResult
        {
            Found = true,
            FilePath = path,
            DispenseTime = time,
            Error = time.HasValue ? null : "找到报文，但未解析到点胶时间",
            FilesScanned = scanned,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    private static (bool found, DateTime? time) TryRead(string path, string housingCode, int maxBytes, bool requireCodeInContent)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 8)
                return (false, null);

            var take = (int)Math.Min(info.Length, maxBytes);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buf = new byte[take];
            var n = fs.Read(buf, 0, take);
            var text = Decode(buf, n);
            if (requireCodeInContent && text.IndexOf(housingCode, StringComparison.OrdinalIgnoreCase) < 0)
                return (false, null);
            return (true, ExtractTime(text));
        }
        catch
        {
            return (false, null);
        }
    }

    private static string Decode(byte[] buf, int n)
    {
        if (n >= 3 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF)
            return Encoding.UTF8.GetString(buf, 3, n - 3);
        if (n >= 2 && buf[0] == 0xFF && buf[1] == 0xFE)
            return Encoding.Unicode.GetString(buf, 2, n - 2);
        return Encoding.UTF8.GetString(buf, 0, n);
    }

    private static DateTime? ExtractTime(string text)
    {
        var m = LabeledTime.Match(text);
        if (m.Success && TryParseTime(m.Groups[1].Value, out var labeled))
            return labeled;

        DateTime? best = null;
        foreach (Match hit in AnyTime.Matches(text))
        {
            if (!TryParseTime(hit.Groups[1].Value, out var dt))
                continue;
            if (best == null || dt > best)
                best = dt;
        }
        return best;
    }

    private static bool TryParseTime(string raw, out DateTime dt)
    {
        var s = raw.Trim().Replace('T', ' ').Replace('/', '-');
        return DateTime.TryParseExact(
            s,
            ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff"],
            null,
            System.Globalization.DateTimeStyles.None,
            out dt);
    }
}
