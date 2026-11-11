using System.Text.Json;
namespace FlexTimeChecker.Core;
public sealed record StationRate(string Key,string Name,bool Complete,int Produced,int Rework);
public sealed record ReworkState
{
    public int SchemaVersion {get;init;}=1;
    public Dictionary<string,List<string>> HousingsByStation {get;init;}=new(StringComparer.OrdinalIgnoreCase);
}
// The UI serializes all calls. Workers NEVER write persistent rework state.
public static class ReworkStore
{
    public static List<StationRate> Apply(string path,ScanResult scan,bool record)
    {
        var state=File.Exists(path) ? JsonSerializer.Deserialize<ReworkState>(ConfigStore.ReadLimited(path,16777216),ConfigStore.Json) ?? throw new InvalidDataException("返工状态为空") : new();
        if(state.SchemaVersion!=1 || state.HousingsByStation is null || state.HousingsByStation.Count>32 || state.HousingsByStation.Sum(k=>k.Value?.Count??0)>200000) throw new InvalidDataException("返工状态版本/限额错误");
        var existing=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach(var pair in state.HousingsByStation)
        {
            if(pair.Key.Length>1024||pair.Value is null||pair.Value.Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>512)) throw new InvalidDataException("返工状态字段错误");
            existing.Add(pair.Key,new(pair.Value,StringComparer.OrdinalIgnoreCase));
        }
        var rates=new List<StationRate>();
        // No snapshot for a station is not evidence that its reports were deleted.
        var updated=existing.ToDictionary(p=>p.Key,p=>p.Value.Order(StringComparer.OrdinalIgnoreCase).ToList(),StringComparer.OrdinalIgnoreCase);
        foreach(var station in scan.Stations)
        {
            var live=new HashSet<string>(station.Housings,StringComparer.OrdinalIgnoreCase);
            var kept=existing.GetValueOrDefault(station.Key)??new(StringComparer.OrdinalIgnoreCase);
            if(station.Complete) kept.IntersectWith(live);
            if(record && scan.Query.Found && scan.Query.DispenseStation==station.Key && live.Contains(scan.Query.Housing)) kept.Add(scan.Query.Housing);
            updated[station.Key]=kept.Order(StringComparer.OrdinalIgnoreCase).ToList();
            rates.Add(new(station.Key,station.Name,station.Complete,live.Count,station.Complete?kept.Count:0));
        }
        if(updated.Count>32 || updated.Sum(p=>p.Value.Count)>200000) throw new InvalidDataException("含未扫描保留站的返工状态超过 32 站 / 200000 条限额；原文件保留，请人工备份并核查历史站位");
        var text=JsonSerializer.Serialize(new ReworkState{HousingsByStation=updated},ConfigStore.Json);
        if(text.Length>16777216) throw new InvalidDataException("返工状态超过 16 MiB，请减少保留报文数");
        var previous=JsonSerializer.Serialize(state,ConfigStore.Json);
        if(text!=previous || !File.Exists(path)) AtomicFile.Write(path,text);
        return rates;
    }
}
