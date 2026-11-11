using System.Text.Json;
using System.Text.Json.Serialization;
namespace FlexExtractor;
public sealed class Station {
 public string StationName {get;set;}="0031";
 [JsonPropertyName("Station")] public string Token {get;set;}="0031";
 public string SaveFolder {get;set;}="";
 public List<string> SubFolders {get;set;}=[];
 public override string ToString()=> $"{StationName} [{Token}] → {SaveFolder}";
}
public sealed class Config {
 public int SchemaVersion {get;set;}=2;
 public string WatchRoot {get;set;}="";
 public List<Station> DispenseItems {get;set;}=[];
 public List<Station> BindItems {get;set;}=[];
 public int PollIntervalSec {get;set;}=1;
 public int LookbackDays {get;set;}=2;
 public int KeepDays {get;set;}=1;
 public bool OnlyNewAfterStart {get;set;}
 public int NewFileGraceSec {get;set;}=2;
 public int MinFileBytes {get;set;}=16;
 public int SizeStableChecks {get;set;}=1;
 public int SizeStableIntervalMs {get;set;}=120;
 public int MaxLogLines {get;set;}=500;
 public int WorkerTimeoutSec {get;set;}=60;
 public long MaxFileBytes {get;set;}=16777216;
 public int MaxFilesPerPass {get;set;}=200;
 [JsonIgnore] public IEnumerable<Station> AllItems=>DispenseItems.Concat(BindItems);
 public static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=16};
 public static Config Parse(string text,bool legacy=false,string? baseDir=null) {
  if(System.Text.Encoding.UTF8.GetByteCount(text)>65536) throw new InvalidDataException("配置超过64KiB");
  using var doc=JsonDocument.Parse(text,new(){MaxDepth=16});
  if(doc.RootElement.ValueKind!=JsonValueKind.Object) throw new JsonException("配置必须是对象");
  if(!legacy&&!doc.RootElement.EnumerateObject().Any(p=>p.Name.Equals("SchemaVersion",StringComparison.OrdinalIgnoreCase)))throw new JsonException("缺少SchemaVersion；旧版配置必须显式导入");
  // Duplicate keys are ambiguous; reject even case variants.
  ValidateKeys(doc.RootElement);
  if(legacy) {
   var node=System.Text.Json.Nodes.JsonNode.Parse(text)!.AsObject();
   var legacyKeys=new[]{"OutputRoot","DispenseStations","BindStations","Stations"};
   string output=baseDir??AppContext.BaseDirectory;
   var outputKey=node.Select(p=>p.Key).FirstOrDefault(k=>k.Equals("OutputRoot",StringComparison.OrdinalIgnoreCase));
   if(outputKey!=null) {output=node[outputKey]!.GetValue<string>(); if(string.IsNullOrWhiteSpace(output))output=Path.Combine(baseDir??AppContext.BaseDirectory,"报文缓存");}
   else output=Path.Combine(output,"报文缓存");
   List<string> Tokens(string name) {var key=node.Select(p=>p.Key).FirstOrDefault(k=>k.Equals(name,StringComparison.OrdinalIgnoreCase)); return key==null?[]:node[key]!.AsArray().Select(x=>x!.GetValue<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();}
   var d=Tokens("DispenseStations");var b=Tokens("BindStations");var old=Tokens("Stations");
   foreach(var key in node.Select(p=>p.Key).Where(k=>legacyKeys.Contains(k,StringComparer.OrdinalIgnoreCase)).ToArray())node.Remove(key);
   var cfg=JsonSerializer.Deserialize<Config>(node.ToJsonString(),Json)??throw new JsonException("空配置");
   cfg.DispenseItems??=[];cfg.BindItems??=[];
   if(cfg.DispenseItems.Count==0&&cfg.BindItems.Count==0) {if(d.Count==0&&b.Count==0)d=old;cfg.DispenseItems=d.Select(t=>new Station{Token=t,StationName=t,SaveFolder=Path.Combine(output,"点胶")}).ToList();cfg.BindItems=b.Select(t=>new Station{Token=t,StationName=t,SaveFolder=Path.Combine(output,"组装")}).ToList();}
   string LegacyPath(string? raw){if(string.IsNullOrWhiteSpace(raw))return "";var p=raw.Trim().Replace('/','\\');if(p.StartsWith('\\')&&!p.StartsWith(@"\\"))p="\\"+p;if(p.StartsWith(@"\\")){var body=p[2..];while(body.Contains(@"\\"))body=body.Replace(@"\\",@"\");return @"\\"+body.TrimEnd('\\');}return Path.GetFullPath(p,baseDir??AppContext.BaseDirectory).TrimEnd('\\');}
   cfg.WatchRoot=LegacyPath(cfg.WatchRoot);output=LegacyPath(output);
   foreach(var s in cfg.AllItems) {if(s==null)throw new JsonException("旧站位为空");if(string.IsNullOrWhiteSpace(s.SaveFolder))s.SaveFolder=Path.Combine(output,cfg.BindItems.Contains(s)?"组装":"点胶");s.SaveFolder=LegacyPath(s.SaveFolder);s.Token=s.Token.Trim();s.StationName=s.Token;s.SubFolders=(s.SubFolders??[]).Select(x=>(x??"").Trim().Replace('\\','/').Trim('/')).Where(x=>x.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();}
   cfg.KeepDays=Math.Clamp(cfg.KeepDays<=0?1:cfg.KeepDays,1,14);cfg.LookbackDays=Math.Clamp(Math.Max(cfg.LookbackDays,cfg.KeepDays+1),1,14);
   cfg.PollIntervalSec=Math.Clamp(cfg.PollIntervalSec,1,60);cfg.NewFileGraceSec=Math.Clamp(cfg.NewFileGraceSec,0,30);cfg.MinFileBytes=Math.Max(1,cfg.MinFileBytes);cfg.SizeStableChecks=Math.Clamp(cfg.SizeStableChecks,0,8);cfg.SizeStableIntervalMs=Math.Clamp(cfg.SizeStableIntervalMs,0,2000);cfg.MaxLogLines=Math.Clamp(cfg.MaxLogLines,50,2000);cfg.SchemaVersion=2;return cfg;
  }
  return JsonSerializer.Deserialize<Config>(text,Json)??throw new JsonException("空配置");
 }
 public void Validate() {
  void Range(long n,long a,long b,string key){if(n<a||n>b)throw new InvalidDataException(key+" 超出范围");}
  if(SchemaVersion!=2)throw new InvalidDataException("未知配置版本；旧版请显式导入");
  Range(PollIntervalSec,1,60,nameof(PollIntervalSec));Range(KeepDays,1,14,nameof(KeepDays));Range(LookbackDays,1,14,nameof(LookbackDays));Range(NewFileGraceSec,0,30,nameof(NewFileGraceSec));Range(MinFileBytes,1,16777216,nameof(MinFileBytes));Range(SizeStableChecks,0,8,nameof(SizeStableChecks));Range(SizeStableIntervalMs,0,2000,nameof(SizeStableIntervalMs));Range(MaxLogLines,50,2000,nameof(MaxLogLines));Range(WorkerTimeoutSec,2,600,nameof(WorkerTimeoutSec));Range(MaxFileBytes,MinFileBytes,134217728,nameof(MaxFileBytes));Range(MaxFilesPerPass,1,10000,nameof(MaxFilesPerPass));
  Paths.Full(WatchRoot);Paths.ValidateLocalSource(WatchRoot); if(DispenseItems==null||BindItems==null||!AllItems.Any()||AllItems.Count()>128)throw new InvalidDataException("请配置1到128个站位");
  foreach(var s in AllItems){if(s==null||string.IsNullOrWhiteSpace(s.Token)||s.Token.Length>128||string.IsNullOrWhiteSpace(s.StationName)||s.StationName.Length>128||s.SubFolders==null||s.SubFolders.Count>256)throw new InvalidDataException("站位名称/规则/子目录无效");Paths.Full(s.SaveFolder);if(s.SaveFolder.StartsWith(@"\\")||new DriveInfo(Path.GetPathRoot(s.SaveFolder)!).DriveType==DriveType.Network)throw new InvalidDataException("保存目录必须为本机磁盘");if(Paths.Overlaps(WatchRoot,s.SaveFolder))throw new InvalidDataException("源与保存目录不能相等或互为父子目录");foreach(var sub in s.SubFolders){if(sub==null||sub.Length>512||Path.IsPathRooted(sub)||sub.Replace('\\','/').Split('/').Any(p=>p==".."||p==""))throw new InvalidDataException("子目录必须为安全的相对路径");}}
 }
 static void ValidateKeys(JsonElement node) {
  if(node.ValueKind==JsonValueKind.Object) {var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach(var p in node.EnumerateObject()){if(!seen.Add(p.Name))throw new JsonException("重复字段 "+p.Name); ValidateKeys(p.Value);}}
  if(node.ValueKind==JsonValueKind.Array)foreach(var item in node.EnumerateArray())ValidateKeys(item);
 }
}
