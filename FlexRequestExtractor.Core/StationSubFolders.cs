namespace FlexRequestExtractor;

public static class StationSubFolders
{
    public static List<string> Discover(string watchRoot, string station, int lookbackDays)
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var root = PathUtil.Normalize(watchRoot);
        var token = (station ?? "").Trim();
        if (string.IsNullOrWhiteSpace(root) || token.Length == 0)
            return [];
        if (!Directory.Exists(root))
            return [];

        var lookback = Math.Max(1, lookbackDays);
        var dateDirs = new List<string>();
        for (var i = 0; i < lookback; i++)
        {
            var day = DateTime.Now.Date.AddDays(-i);
            var path = Path.Combine(root, day.ToString("yyyy"), day.ToString("MM"), day.ToString("dd"));
            if (Directory.Exists(path))
                dateDirs.Add(path);
        }
        if (dateDirs.Count == 0)
            dateDirs.Add(root);

        foreach (var dateDir in dateDirs)
        {
            IEnumerable<string> stations;
            try { stations = Directory.EnumerateDirectories(dateDir); }
            catch { continue; }

            foreach (var stationDir in stations)
            {
                string name;
                try { name = Path.GetFileName(stationDir.TrimEnd('\\', '/')); }
                catch { continue; }
                if (!StationRules.Matches(name, [token]))
                    continue;
                Collect(stationDir, stationDir, found);
            }
        }

        return found.ToList();
    }

    public static bool FileMatches(string stationDir, string file, IReadOnlyList<string>? subFolders)
    {
        if (subFolders is null || subFolders.Count == 0)
            return true;

        string parent;
        try { parent = Path.GetDirectoryName(file) ?? ""; }
        catch { return false; }

        string rel;
        try { rel = Path.GetRelativePath(stationDir, parent).Replace('\\', '/'); }
        catch { return false; }

        if (rel == "." || string.IsNullOrWhiteSpace(rel))
            return subFolders.Any(IsRoot);

        foreach (var raw in subFolders)
        {
            var sel = NormalizeKey(raw);
            if (sel.Length == 0 || IsRoot(sel))
                continue;
            if (rel.Equals(sel, StringComparison.OrdinalIgnoreCase)
                || rel.StartsWith(sel + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static List<string> NormalizeList(IEnumerable<string>? raw) =>
        (raw ?? [])
            .Select(NormalizeKey)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string NormalizeKey(string? raw)
    {
        var s = (raw ?? "").Trim().Replace('\\', '/').Trim('/');
        return s == "." ? "." : s;
    }

    private static bool IsRoot(string? s)
    {
        var v = NormalizeKey(s);
        return v.Length == 0 || v == ".";
    }

    private static void Collect(string stationDir, string current, SortedSet<string> found)
    {
        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(current); }
        catch { return; }

        foreach (var dir in dirs)
        {
            string rel;
            try { rel = Path.GetRelativePath(stationDir, dir).Replace('\\', '/'); }
            catch { continue; }
            if (!string.IsNullOrWhiteSpace(rel) && rel != ".")
                found.Add(rel);
            Collect(stationDir, dir, found);
        }
    }
}
