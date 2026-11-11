using System.Text.Json;
using System.Text.Json.Serialization;
namespace FlexTimeChecker.Core;
public sealed record FolderProfile
{
    public string Name { get; init; } = "新站位";
    public string MessageFolder { get; init; } = "";
    public string Role { get; init; } = "Dispense";
    public int DemolishMinutes { get; init; } = 120;
    public int ClearGlueMinutes { get; init; } = 240;
    [JsonIgnore] public bool IsBind => Role == "Bind";
    [JsonIgnore] public string Key => Path.GetFullPath(MessageFolder).TrimEnd('\\','/').ToUpperInvariant();
}
public sealed record AppConfig
{
    public int SchemaVersion { get; init; } = 1;
    public List<FolderProfile> Folders { get; init; } = [];
    public int WorkerTimeoutSeconds { get; init; } = 20;
}
public static class ConfigStore
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive=true, WriteIndented=true, UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow, MaxDepth=32 };
    static readonly JsonSerializerOptions LegacyJson = new(Json) { ReadCommentHandling=JsonCommentHandling.Skip, AllowTrailingCommas=true };
    public static AppConfig Parse(string text, bool explicitLegacyImport)
    {
        if(text.Length>262144) throw new InvalidDataException("配置超过 256 KiB");
        using var document=JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth=32, CommentHandling=explicitLegacyImport?JsonCommentHandling.Skip:JsonCommentHandling.Disallow, AllowTrailingCommas=explicitLegacyImport });
        var root=document.RootElement;
        RejectDuplicates(root);
        if(root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("配置不是对象");
        var names=root.EnumerateObject().Select(x=>x.Name).ToList();
        if(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=names.Count) throw new InvalidDataException("重复配置字段");
        JsonElement? Find(string name) { foreach(var p in root.EnumerateObject()) if(p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)) return p.Value; return null; }
        var version=Find("SchemaVersion");
        AppConfig cfg;
        if(version is null)
        {
            if(!explicitLegacyImport) throw new InvalidDataException("旧版配置须在配置窗口显式导入，原文件不会被覆盖");
            var allowed=new HashSet<string>(["MessageFolder","DemolishMinutes","ClearGlueMinutes","MaxReadBytes","FileExtensions","Folders"],StringComparer.OrdinalIgnoreCase);
            if(names.Any(n=>!allowed.Contains(n))) throw new InvalidDataException("旧版配置包含未知字段");
            var folders=Find("Folders");
            if(folders is not null && folders.Value.ValueKind!=JsonValueKind.Null && (folders.Value.ValueKind!=JsonValueKind.Array || folders.Value.GetArrayLength()>0)) cfg=new(){Folders=folders.Value.Deserialize<List<FolderProfile>>(LegacyJson) ?? throw new InvalidDataException("Folders 为空")};
            else
            {
                var folder=Find("MessageFolder");
                if(folder is null) throw new InvalidDataException("不认识的旧版配置");
                if(folder.Value.ValueKind!=JsonValueKind.String) throw new InvalidDataException("MessageFolder 必须为字符串");
                int Minutes(string name,int fallback)
                {
                    var value=Find(name); if(value is null) return fallback;
                    if(value.Value.ValueKind!=JsonValueKind.Number||!value.Value.TryGetInt32(out var result)) throw new InvalidDataException(name+" 必须为范围内整数");
                    return result;
                }
                cfg=new(){ Folders=[new(){Name="导入点胶站",MessageFolder=folder.Value.GetString()!,DemolishMinutes=Minutes("DemolishMinutes",120),ClearGlueMinutes=Minutes("ClearGlueMinutes",240)}] };
            }
            if(cfg.Folders.Any(f=>f is null||string.IsNullOrWhiteSpace(f.Role))) throw new InvalidDataException("旧版站位或类型为空");
            cfg=cfg with {Folders=cfg.Folders.Select(f=>f with {Role=f.Role.Trim()}).ToList()};
            cfg=cfg with {Folders=cfg.Folders.Select(f=>f with {Role=f.Role.Equals("Bind",StringComparison.OrdinalIgnoreCase)||f.Role.Equals("Assembly",StringComparison.OrdinalIgnoreCase)||f.Role.Contains("组装")||f.Role.Contains("绑定")?"Bind":f.Role.Equals("Dispense",StringComparison.OrdinalIgnoreCase)?"Dispense":throw new InvalidDataException("未知站位类型")}).ToList()};
        }
        else cfg=JsonSerializer.Deserialize<AppConfig>(text,Json) ?? throw new InvalidDataException("空配置");
        Validate(cfg); return cfg;
    }
    static void RejectDuplicates(JsonElement node)
    {
        if(node.ValueKind==JsonValueKind.Object)
        {
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var property in node.EnumerateObject()) { if(!seen.Add(property.Name)) throw new InvalidDataException("重复配置字段"); RejectDuplicates(property.Value); }
        }
        else if(node.ValueKind==JsonValueKind.Array) foreach(var child in node.EnumerateArray()) RejectDuplicates(child);
    }
    public static void Validate(AppConfig cfg)
    {
        if(cfg.SchemaVersion!=1 || cfg.Folders is null || cfg.Folders.Count>32 || cfg.WorkerTimeoutSeconds is <1 or >120) throw new InvalidDataException("配置版本或数量/超时限额错误");
        var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var f in cfg.Folders)
        {
            if(f is null || string.IsNullOrWhiteSpace(f.Name)|| f.Name.Length>64 || string.IsNullOrWhiteSpace(f.MessageFolder)|| f.MessageFolder.Length>1024 || !Path.IsPathFullyQualified(f.MessageFolder) || f.Role is not ("Dispense" or "Bind") || f.DemolishMinutes is <0 or >525600 || f.ClearGlueMinutes is <0 or >525600) throw new InvalidDataException("站位名称、绝对目录、类型或分钟限额错误");
            if(!keys.Add(f.Key)) throw new InvalidDataException("站位目录不可重复（含不同角色）");
        }
    }
    public static AppConfig Load(string path) => Parse(ReadLimited(path,262144),false);
    public static void Save(string path,AppConfig cfg) { Validate(cfg); AtomicFile.Write(path,JsonSerializer.Serialize(cfg,Json)); }
    public static string ReadLimited(string path,int maxBytes)
    {
        using var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(s.Length>maxBytes) throw new InvalidDataException("文件超过安全限额");
        using var r=new StreamReader(s); var text=r.ReadToEnd(); if(text.Length>maxBytes) throw new InvalidDataException("文件超过安全限额"); return text;
    }
}
public static class AtomicFile
{
    public static void Write(string path,string content)
    {
        var full=Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var s=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.WriteThrough))
            { using var w=new StreamWriter(s,leaveOpen:true); w.Write(content); w.Flush(); s.Flush(true); }
            if(File.Exists(full)) File.Replace(temp,full,null); else File.Move(temp,full);
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
}
