using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexTimeChecker;

internal sealed class AppConfig
{
    public string MessageFolder { get; set; } = @"E:\Download\AI\Flex 点胶时间查询\报文文件夹";
    public int DemolishMinutes { get; set; } = 120;
    public int ClearGlueMinutes { get; set; } = 240;
    public int MaxReadBytes { get; set; } = 262144;
    public string[] FileExtensions { get; set; } = [".txt", ".log", ".csv", ".xml", ".json", ".dat"];
    public List<FolderProfile> Folders { get; set; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string AppDir
    {
        get
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                    return dir;
            }
            return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    public static string ConfigPath => Path.Combine(AppDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (cfg != null)
                {
                    cfg.Normalize();
                    return cfg;
                }
            }
        }
        catch
        {
            // fall through to defaults
        }

        var fresh = new AppConfig();
        fresh.Normalize();
        return fresh;
    }

    public void Save()
    {
        Normalize();
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public IReadOnlyList<FolderProfile> GetProfiles() => Folders;

    public IReadOnlyList<FolderProfile> GetDispenseProfiles()
        => Folders.Where(x => !x.IsBind).ToList();

    public IReadOnlyList<FolderProfile> GetBindProfiles()
        => Folders.Where(x => x.IsBind).ToList();

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(MessageFolder))
            MessageFolder = @"E:\Download\AI\Flex 点胶时间查询\报文文件夹";
        if (DemolishMinutes < 0) DemolishMinutes = 0;
        if (ClearGlueMinutes < 0) ClearGlueMinutes = 0;
        if (MaxReadBytes < 4096) MaxReadBytes = 4096;
        if (FileExtensions is null || FileExtensions.Length == 0)
            FileExtensions = [".txt", ".log", ".csv", ".xml", ".json", ".dat"];
        FileExtensions = FileExtensions
            .Select(x => x.StartsWith('.') ? x.ToLowerInvariant() : "." + x.ToLowerInvariant())
            .Distinct()
            .ToArray();

        // 兼容早期的单目录 config.json：首次读取时自动转换为一条目录配置。
        if (Folders is null || Folders.Count == 0)
        {
            Folders =
            [
                new FolderProfile
                {
                    Name = "默认目录",
                    MessageFolder = MessageFolder,
                    DemolishMinutes = DemolishMinutes,
                    ClearGlueMinutes = ClearGlueMinutes
                }
            ];
        }
        foreach (var folder in Folders)
            folder.Normalize();
        Folders = Folders.Where(x => !string.IsNullOrWhiteSpace(x.MessageFolder)).ToList();
    }
}
