using System.Diagnostics;
using System.Xml;
namespace FlexTimeChecker.Core;
public sealed record QueryResult
{
    public bool Found {get;init;}
    public string Housing {get;init;}="";
    public string? Material {get;init;}
    public DateTimeOffset Stamp {get;init;}
    public string DispenseStation {get;init;}="";
    public string ThresholdStation {get;init;}="";
    public string SourceFile {get;init;}="";
    public string Error {get;init;}="未找到";
}
public sealed record StationInventory(string Key,string Name,bool Complete,List<string> Housings);
public sealed record ScanResult(QueryResult Query,List<StationInventory> Stations,List<string> Diagnostics,int Entries,long ElapsedMs);
public static class SearchEngine
{
    const int MaxEntries=200000;
    sealed record Hit(FolderProfile Profile,string Path,RequestRecord Record);
    public static ScanResult Run(AppConfig cfg,string code)
    {
        ConfigStore.Validate(cfg); code=code.Trim();
        if(code.Length>512) throw new InvalidDataException("扫码长度超过 512");
        var sw=Stopwatch.StartNew(); var diagnostics=new List<string>(); var entries=0;
        var stations=new List<StationInventory>();
        var latest=new Dictionary<string,Hit>(StringComparer.OrdinalIgnoreCase);
        foreach(var profile in cfg.Folders.Where(f=>!f.IsBind))
        {
            var live=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var complete=Visit(profile,code,ref entries,diagnostics,(path,record)=>{
                live.Add(record.Housing); var hit=new Hit(profile,path,record);
                if(!latest.TryGetValue(record.Housing,out var previous)||Better(hit,previous)) latest[record.Housing]=hit;
            });
            stations.Add(new(profile.Key,profile.Name,complete,live.Order(StringComparer.OrdinalIgnoreCase).ToList()));
        }
        QueryResult query=new();
        if(cfg.Folders.Count==0 && code.Length>0) return new(new(){Error="未配置报文目录，请打开左上角配置，显式导入旧配置或添加站位"},stations,diagnostics,entries,sw.ElapsedMilliseconds);
        if(code.Length>0 && stations.Any(s=>!s.Complete)) query=new(){Error="点胶目录扫描不完整，无法确认最新点胶时间；禁止放行，请检查诊断并重试"};
        else if(code.Length>0 && latest.TryGetValue(code,out var direct)) query=Success(direct,direct.Profile,null);
        else if(code.Length>0)
        {
            Hit? binding=null; var complete=true;
            foreach(var profile in cfg.Folders.Where(f=>f.IsBind))
                complete &= Visit(profile,code,ref entries,diagnostics,(path,record)=>{
                    var hit=new Hit(profile,path,record);
                    if(record.MatchesMaterial && (binding is null||Better(hit,binding))) binding=hit;
                });
            if(!complete) query=new(){Error="组装目录扫描不完整，无法确认最新绑定；禁止放行"};
            else if(binding is not null)
                query=latest.TryGetValue(binding.Record.Housing,out var dispense) ? Success(dispense,binding.Profile,code) : new(){Housing=binding.Record.Housing,Material=code,Error="物料已绑定 Housing，但点胶站未找到点胶时间"};
        }
        return new(query,stations,diagnostics,entries,sw.ElapsedMilliseconds);
    }
    static QueryResult Success(Hit hit,FolderProfile threshold,string? material) => new(){Found=true,Housing=hit.Record.Housing,Material=material,Stamp=hit.Record.Stamp,DispenseStation=hit.Profile.Key,ThresholdStation=threshold.Key,SourceFile=hit.Path,Error=""};
    static bool Better(Hit candidate,Hit previous)
    {
        var time=candidate.Record.Stamp.CompareTo(previous.Record.Stamp);
        if(time!=0) return time>0;
        var station=StringComparer.OrdinalIgnoreCase.Compare(candidate.Profile.Key,previous.Profile.Key);
        if(station!=0) return station<0;
        var path=StringComparer.OrdinalIgnoreCase.Compare(candidate.Path,previous.Path);
        return path!=0 ? path<0 : StringComparer.Ordinal.Compare(candidate.Path,previous.Path)<0;
    }
    static bool Visit(FolderProfile profile,string code,ref int entries,List<string> diagnostics,Action<string,RequestRecord> accept)
    {
        var complete=true; var pending=new Stack<(string Path,int Depth)>(); pending.Push((profile.MessageFolder,0));
        void Error(string message) { complete=false; if(diagnostics.Count<50) diagnostics.Add(profile.Name+": "+message); }
        while(pending.TryPop(out var dir))
        {
            if(entries>=MaxEntries) { Error("条目数超过 200000 限额"); break; }
            IEnumerator<string>? enumerator=null;
            try
            {
                if((File.GetAttributes(dir.Path)&FileAttributes.ReparsePoint)!=0) { Error("跳过 junction / 重解析目录"); continue; }
                enumerator=Directory.EnumerateFileSystemEntries(dir.Path).GetEnumerator();
                while(enumerator.MoveNext())
                {
                    if(++entries>MaxEntries) { Error("条目数超过 200000 限额"); break; }
                    var path=enumerator.Current;
                    try
                    {
                        var attributes=File.GetAttributes(path);
                        if((attributes&FileAttributes.ReparsePoint)!=0) { Error("跳过重解析条目"); continue; }
                        if((attributes&FileAttributes.Directory)!=0)
                        {
                            if(dir.Depth>=64||pending.Count>=4096) Error("子目录深度/待扫描目录超过限额");
                            else pending.Push((path,dir.Depth+1));
                        }
                        else if(Path.GetFileName(path).StartsWith("Request",StringComparison.OrdinalIgnoreCase)&&Path.GetExtension(path).Equals(".xml",StringComparison.OrdinalIgnoreCase))
                            accept(path,RequestReader.Read(path,code));
                    }
                    catch(Exception ex) when(IsExpectedIo(ex)) { Error(ex.GetType().Name+"（报文不可读/变化/不安全）"); }
                }
            }
            catch(Exception ex) when(IsExpectedIo(ex)) { Error(ex.GetType().Name+"（目录枚举失败）"); }
            finally { enumerator?.Dispose(); }
        }
        return complete;
    }
    public static bool IsExpectedIo(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or XmlException or System.Security.SecurityException;
}
