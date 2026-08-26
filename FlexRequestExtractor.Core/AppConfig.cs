using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexRequestExtractor;

public sealed class AppConfig
{
    public const string ServiceName = "FlexRequestExtractor";
    public const string DispenseFolderName = "点胶";
    public const string BindFolderName = "组装";

    public string WatchRoot { get; set; } = "";
    public string OutputRoot { get; set; } = "";
    public List<StationTarget> DispenseItems { get; set; } = [];
    public List<StationTarget> BindItems { get; set; } = [];
    public List<string> DispenseStations { get; set; } = [];
    public List<string> BindStations { get; set; } = [];
    public List<string> Stations { get; set; } = [];
    public int PollIntervalSec { get; set; } = 1;
    public int LookbackDays { get; set; } = 2;
    public int KeepDays { get; set; } = 1;
    public bool OnlyNewAfterStart { get; set; } = false;
    public int NewFileGraceSec { get; set; } = 2;
    public int MinFileBytes { get; set; } = 16;
    public int SizeStableChecks { get; set; } = 1;
    public int SizeStableIntervalMs { get; set; } = 120;
    public int MaxLogLines { get; set; } = 500;

    public static string AppDir => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    public static string ConfigPath => Path.Combine(AppDir, "config.json");

    [JsonIgnore]
    public IReadOnlyList<StationTarget> AllItems => DispenseItems.Concat(BindItems).ToList();

    [JsonIgnore]
    public IReadOnlyList<string> AllSaveFolders =>
        AllItems
            .Select(x => x.SaveFolder)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppConfig Load()
    {
        AppConfig cfg;
        if (!File.Exists(ConfigPath))
            cfg = new AppConfig();
        else
        {
            try
            {
                cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOptions) ?? new AppConfig();
            }
            catch
            {
                cfg = new AppConfig();
            }
        }
        cfg.Normalize();
        return cfg;
    }

    public void Save()
    {
        Normalize();
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public AppConfig Clone()
    {
        return new AppConfig
        {
            WatchRoot = WatchRoot,
            OutputRoot = OutputRoot,
            DispenseItems = DispenseItems.Select(x => x.Clone()).ToList(),
            BindItems = BindItems.Select(x => x.Clone()).ToList(),
            DispenseStations = DispenseStations.ToList(),
            BindStations = BindStations.ToList(),
            Stations = Stations.ToList(),
            PollIntervalSec = PollIntervalSec,
            LookbackDays = LookbackDays,
            KeepDays = KeepDays,
            OnlyNewAfterStart = OnlyNewAfterStart,
            NewFileGraceSec = NewFileGraceSec,
            MinFileBytes = MinFileBytes,
            SizeStableChecks = SizeStableChecks,
            SizeStableIntervalMs = SizeStableIntervalMs,
            MaxLogLines = MaxLogLines
        };
    }

    public string DefaultSaveFolder(bool bind)
    {
        var root = string.IsNullOrWhiteSpace(OutputRoot) ? Path.Combine(AppDir, "报文缓存") : OutputRoot;
        return Path.Combine(root, bind ? BindFolderName : DispenseFolderName);
    }

    public IEnumerable<StationTarget> MatchDispense(string stationFolder) =>
        DispenseItems.Where(x => StationRules.Matches(stationFolder, [x.Station]));

    public IEnumerable<StationTarget> MatchBind(string stationFolder) =>
        BindItems.Where(x => StationRules.Matches(stationFolder, [x.Station]));

    public void Normalize()
    {
        WatchRoot = PathUtil.Normalize(WatchRoot);
        OutputRoot = PathUtil.Normalize(OutputRoot);
        if (string.IsNullOrWhiteSpace(OutputRoot))
            OutputRoot = Path.Combine(AppDir, "报文缓存");

        DispenseItems ??= [];
        BindItems ??= [];
        DispenseStations = Clean(DispenseStations);
        BindStations = Clean(BindStations);
        Stations = Clean(Stations);

        if (DispenseItems.Count == 0 && BindItems.Count == 0)
        {
            foreach (var token in DispenseStations)
                DispenseItems.Add(NewItem(token, false));
            foreach (var token in BindStations)
                BindItems.Add(NewItem(token, true));
            if (DispenseItems.Count == 0 && BindItems.Count == 0)
            {
                foreach (var token in Stations)
                    DispenseItems.Add(NewItem(token, false));
            }
        }

        foreach (var item in DispenseItems)
        {
            item.Normalize();
            if (string.IsNullOrWhiteSpace(item.SaveFolder))
                item.SaveFolder = DefaultSaveFolder(false);
        }
        foreach (var item in BindItems)
        {
            item.Normalize();
            if (string.IsNullOrWhiteSpace(item.SaveFolder))
                item.SaveFolder = DefaultSaveFolder(true);
        }

        DispenseItems = DispenseItems.Where(x => x.Station.Length > 0).ToList();
        BindItems = BindItems.Where(x => x.Station.Length > 0).ToList();
        DispenseStations = DispenseItems.Select(x => x.Station).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        BindStations = BindItems.Select(x => x.Station).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Stations = DispenseStations.Concat(BindStations).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        PollIntervalSec = Math.Clamp(PollIntervalSec, 1, 60);
        KeepDays = Math.Clamp(KeepDays <= 0 ? 1 : KeepDays, 1, 14);
        LookbackDays = Math.Clamp(Math.Max(LookbackDays, KeepDays + 1), 1, 14);
        NewFileGraceSec = Math.Clamp(NewFileGraceSec, 0, 30);
        MinFileBytes = Math.Max(1, MinFileBytes);
        SizeStableChecks = Math.Clamp(SizeStableChecks, 0, 8);
        SizeStableIntervalMs = Math.Clamp(SizeStableIntervalMs, 0, 2000);
        MaxLogLines = Math.Clamp(MaxLogLines, 50, 2000);
    }

    private StationTarget NewItem(string station, bool bind) => new()
    {
        Station = station,
        SaveFolder = DefaultSaveFolder(bind)
    };

    private static List<string> Clean(IEnumerable<string>? raw) =>
        (raw ?? [])
            .Select(x => (x ?? "").Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
