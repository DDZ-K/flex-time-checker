using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexTimeChecker;

internal sealed class StationRate
{
    public required string Name { get; init; }
    public required string Folder { get; init; }
    public int Produced { get; init; }
    public int Rework { get; init; }
    public double Rate => Produced <= 0 ? 0 : (double)Rework / Produced;
}

internal sealed class ReworkState
{
    public string Date { get; set; } = "";
    public Dictionary<string, List<string>> HousingsByFolder { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal static class ReworkTracker
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DefaultPath => Path.Combine(AppConfig.AppDir, "rework.json");

    public static IReadOnlyList<StationRate> Snapshot(AppConfig cfg, string? storePath = null)
    {
        var state = Load(storePath);
        var list = new List<StationRate>();
        var dirty = false;
        foreach (var profile in cfg.GetDispenseProfiles())
        {
            var folder = (profile.MessageFolder ?? "").Trim();
            var live = CollectHousings(folder);
            var key = FolderKey(folder);
            state.HousingsByFolder.TryGetValue(key, out var recorded);
            var kept = new List<string>();
            if (recorded != null)
            {
                foreach (var housing in recorded)
                {
                    if (housing.Length == 0)
                        continue;
                    if (!live.Contains(housing))
                        continue;
                    if (kept.Any(x => x.Equals(housing, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    kept.Add(housing);
                }
                if (kept.Count != recorded.Count)
                {
                    state.HousingsByFolder[key] = kept;
                    dirty = true;
                }
            }

            list.Add(new StationRate
            {
                Name = profile.Name,
                Folder = folder,
                Produced = live.Count,
                Rework = kept.Count
            });
        }
        if (dirty)
            Save(state, storePath);
        return list;
    }

    public static bool TryRecord(SearchResult result, AppConfig cfg, string? storePath = null)
    {
        if (!result.Found)
            return false;
        var housing = (result.HousingCode ?? "").Trim();
        if (housing.Length == 0)
            return false;
        var file = result.FilePath;
        if (string.IsNullOrWhiteSpace(file))
            return false;

        FolderProfile? hit = null;
        foreach (var profile in cfg.GetDispenseProfiles())
        {
            if (FileBelongsTo(file, profile.MessageFolder))
            {
                hit = profile;
                break;
            }
        }
        if (hit is null)
            return false;

        var path = storePath ?? DefaultPath;
        var state = Load(path);
        var key = FolderKey(hit.MessageFolder);
        if (!state.HousingsByFolder.TryGetValue(key, out var list) || list is null)
        {
            list = [];
            state.HousingsByFolder[key] = list;
        }
        if (list.Any(x => x.Equals(housing, StringComparison.OrdinalIgnoreCase)))
            return false;
        list.Add(housing);
        Save(state, path);
        return true;
    }

    public static int CountProduced(string folder) => CollectHousings(folder).Count;

    public static HashSet<string> CollectHousings(string folder)
    {
        var housings = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder, "Request*.xml", SearchOption.AllDirectories);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        Parallel.ForEach(files, path =>
        {
            if (!RequestXmlSearch.TryReadRequest(path, out var housing, out _, out _))
                return;
            if (housing.Length > 0)
                housings.TryAdd(housing, 0);
        });
        return housings.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool FileBelongsTo(string filePath, string folder)
    {
        try
        {
            var file = Path.GetFullPath(filePath);
            var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (file.Equals(root, StringComparison.OrdinalIgnoreCase))
                return true;
            var prefix = root + Path.DirectorySeparatorChar;
            return file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    internal static ReworkState Load(string? storePath = null)
    {
        var path = storePath ?? DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var state = JsonSerializer.Deserialize<ReworkState>(File.ReadAllText(path), JsonOptions);
                if (state != null)
                {
                    var bags = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in state.HousingsByFolder ?? [])
                    {
                        if (string.IsNullOrWhiteSpace(kv.Key))
                            continue;
                        bags[kv.Key] = kv.Value?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
                    }
                    state.HousingsByFolder = bags;
                    return state;
                }
            }
        }
        catch
        {
            // broken file → empty
        }
        return new ReworkState { Date = DateTime.Now.ToString("yyyy-MM-dd") };
    }

    internal static void Save(ReworkState state, string? storePath = null)
    {
        var path = storePath ?? DefaultPath;
        state.Date = DateTime.Now.ToString("yyyy-MM-dd");
        File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
    }

    private static string FolderKey(string folder)
        => Path.GetFullPath(string.IsNullOrWhiteSpace(folder) ? "." : folder);
}
