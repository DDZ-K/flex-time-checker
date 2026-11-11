using FlexTimeChecker.Core;
namespace FlexTimeChecker.Tests;
public static partial class SelfTests
{
    static void RegisterRework()
    {
        Tests.Add(("rework retained station quota rejects overflow without corrupting prior state",()=>{
            using var b=new Sandbox(); var path=Path.Combine(b.Root,"rework.json");
            var initial=new ReworkState{HousingsByStation=Enumerable.Range(0,32).ToDictionary(i=>"STATION-"+i,i=>new List<string>{"H"})};
            var original=System.Text.Json.JsonSerializer.Serialize(initial,ConfigStore.Json); File.WriteAllText(path,original);
            Throws(()=>ReworkStore.Apply(path,new(new(),[new("ADDITIONAL","D",true,["NEW"])],[],0,0),false));
            Check(File.ReadAllText(path)==original,"quota rejection must leave saved state readable and unchanged");
        }));
        Tests.Add(("rework retains unscanned stations through empty config removal and restoration",()=>{
            using var b=new Sandbox();
            b.Put("d/Request_h.xml",Xml("H")); b.Put("other/Request_other.xml",Xml("OTHER"));
            var d=new FolderProfile{MessageFolder=Path.Combine(b.Root,"d")};
            var other=new FolderProfile{MessageFolder=Path.Combine(b.Root,"other")};
            var cfg=new AppConfig{Folders=[d,other]}; var path=Path.Combine(b.Root,"rework.json");
            ReworkStore.Apply(path,SearchEngine.Run(cfg,"H"),true);
            ReworkStore.Apply(path,SearchEngine.Run(new(),""),false);
            Check(File.ReadAllText(path).Contains("\"H\""),"empty scan must preserve unscanned station rework");
            ReworkStore.Apply(path,SearchEngine.Run(cfg with{Folders=[other]},""),false);
            Check(ReworkStore.Apply(path,SearchEngine.Run(cfg,""),false).Single(r=>r.Key==d.Key).Rework==1,"removed and restored station retains rework");
            ReworkStore.Apply(path,new(new(),[new(d.Key,d.Name,false,[])],[],0,0),false);
            Check(File.ReadAllText(path).Contains("\"H\""),"incomplete scan retains prior housings");
            File.Delete(Path.Combine(b.Root,"d/Request_h.xml"));
            Check(ReworkStore.Apply(path,SearchEngine.Run(cfg,""),false).Single(r=>r.Key==d.Key).Rework==0,"only complete scanned station prunes actual deletions");
        }));
        Tests.Add(("persistent rework housing dedup dispense ownership and deletion",()=>{
            using var b=new Sandbox();
            b.Put("d/Request_a.xml",Xml("H")); b.Put("d/Request_duplicate.xml",Xml("h"));
            b.Put("bind/Request_b.xml",Xml("H",item:"<item matID=\"M\"/>"));
            var cfg=new AppConfig{Folders=[new(){Name="D",MessageFolder=Path.Combine(b.Root,"d")},new(){Name="B",Role="Bind",MessageFolder=Path.Combine(b.Root,"bind")}]};
            var store=Path.Combine(b.Root,"rework.json");
            var scan=SearchEngine.Run(cfg,"M");
            for(var i=0;i<3;i++)
            { var rates=Tree(Call("ReworkStore","Apply",store,scan,true)); Check(rates[0].GetProperty("Produced").GetInt32()==1&&rates[0].GetProperty("Rework").GetInt32()==1,"dedup / actual dispense ownership persistent"); }
            File.Delete(Path.Combine(b.Root,"d/Request_a.xml")); File.Delete(Path.Combine(b.Root,"d/Request_duplicate.xml"));
            var gone=Tree(Call("ReworkStore","Apply",store,SearchEngine.Run(cfg,""),false));
            Check(gone[0].GetProperty("Produced").GetInt32()==0&&gone[0].GetProperty("Rework").GetInt32()==0,"file deletion drops rework");
            File.WriteAllText(store,"broken"); var before=File.ReadAllText(store);
            Throws(()=>Call("ReworkStore","Apply",store,scan,false)); Check(File.ReadAllText(store)==before,"bad state must not overwrite");
        }));
    }
}
