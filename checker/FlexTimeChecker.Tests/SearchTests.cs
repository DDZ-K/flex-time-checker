using FlexTimeChecker.Core;
namespace FlexTimeChecker.Tests;
public static partial class SelfTests
{
    static void RegisterSearch()
    {
        Tests.Add(("no configured folders returns actionable configuration error",()=>{
            var q=SearchEngine.Run(new(),"H").Query; Check(!q.Found&&q.Error.Contains("未配置"),"empty config must explain next step");
        }));
        Tests.Add(("junction recursion skipped and flagged incomplete",()=>{
            using var b=new Sandbox(); var folder=Path.Combine(b.Root,"d"); Directory.CreateDirectory(folder); b.Put("d/Request_h.xml",Xml("H"));
            var link=Path.Combine(folder,"cycle");
            var psi=new System.Diagnostics.ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"/c","mklink","/J",link,folder}) psi.ArgumentList.Add(arg);
            using var p=System.Diagnostics.Process.Start(psi)!; var output=p.StandardOutput.ReadToEnd(); var error=p.StandardError.ReadToEnd(); p.WaitForExit();
            Check(p.ExitCode==0,"junction fixture creation failed: "+output+error);
            try { var result=SearchEngine.Run(new(){Folders=[new(){MessageFolder=folder}]},"H"); Check(!result.Query.Found&&!result.Stations.Single().Complete&&result.Entries<10,"junction must not recurse or authorize"); }
            finally { Directory.Delete(link); }
        }));
        Tests.Add(("unreadable oversized report fails closed without destroying inventory",()=>{
            using var b=new Sandbox();
            b.Put("d/Request_ok.xml",Xml("H")); b.Put("d/Request_big.xml",Xml(new string('H',513)));
            var cfg=new AppConfig{Folders=[new(){MessageFolder=Path.Combine(b.Root,"d")}]};
            var r=SearchEngine.Run(cfg,"H"); Check(!r.Query.Found&&!r.Stations.Single().Complete&&r.Diagnostics.Count>0,"incomplete scan must not authorize");
            File.Delete(Path.Combine(b.Root,"d/Request_big.xml"));
            Check(SearchEngine.Run(cfg,"H").Query.Found,"recovers after removal");
            cfg=cfg with{Folders=[new(){MessageFolder=Path.Combine(b.Root,"missing")}]};
            Check(!SearchEngine.Run(cfg,"H").Query.Found,"missing directory safe");
        }));
        Tests.Add(("equal timestamp winner deterministic across config reorder",()=>{
            using var b=new Sandbox(); b.Put("a/Request_z.xml",Xml("H")); b.Put("z/Request_a.xml",Xml("H"));
            var cfg=new AppConfig{Folders=[new(){MessageFolder=Path.Combine(b.Root,"z")},new(){MessageFolder=Path.Combine(b.Root,"a")}]};
            var first=SearchEngine.Run(cfg,"H").Query; var second=SearchEngine.Run(cfg with{Folders=cfg.Folders.AsEnumerable().Reverse().ToList()},"H").Query;
            Check(first.DispenseStation==second.DispenseStation && first.DispenseStation==cfg.Folders[1].Key,"tie uses canonical station/path order");
        }));
        Tests.Add(("material latest binding uses bind thresholds separate dispense ownership",()=>{
            using var b=new Sandbox();
            b.Put("d1/Request_old.xml",Xml("OLD")); b.Put("d2/Request_new.xml",Xml("NEW","2026-08-26T11:00:00+08:00"));
            b.Put("bind/Request_old.xml",Xml("OLD",item:"<item value=\"M\"/>"));
            b.Put("bind/Request_new.xml",Xml("NEW","2026-08-26T11:10:00+08:00", "<item customSerial=\"M\"/>"));
            var cfg=new AppConfig {Folders=[new(){Name="D1",MessageFolder=Path.Combine(b.Root,"d1")},new(){Name="D2",MessageFolder=Path.Combine(b.Root,"d2")},new(){Name="B",Role="Bind",MessageFolder=Path.Combine(b.Root,"bind"),DemolishMinutes=15,ClearGlueMinutes=90}]};
            var q=SearchEngine.Run(cfg,"m").Query;
            Check(q.Found && q.Housing=="NEW" && q.Material=="m","latest material binding");
            Check(q.ThresholdStation==cfg.Folders[2].Key && q.DispenseStation==cfg.Folders[1].Key,"ownership split");
            File.Delete(Path.Combine(b.Root,"d2/Request_new.xml"));
            Check(!SearchEngine.Run(cfg,"m").Query.Found,"missing dispense never falls back to older binding");
        }));
        Tests.Add(("recursive Request XML latest station and exact Housing priority",()=>{
            using var b=new Sandbox();
            b.Put("d1/nested/Request_old.xml",Xml("H"));
            b.Put("d2/Request_new.xml",Xml("h","2026-08-26T11:00:00+08:00"));
            b.Put("d1/Other.xml",Xml("H","2027-01-01T00:00:00+08:00"));
            b.Put("bind/Request_bind.xml",Xml("OTHER",item:"<item identifier=\"H\"/>"));
            var cfg=new AppConfig{Folders=[new(){Name="D1",MessageFolder=Path.Combine(b.Root,"d1")},new(){Name="D2",MessageFolder=Path.Combine(b.Root,"d2"),DemolishMinutes=31},new(){Name="B",MessageFolder=Path.Combine(b.Root,"bind"),Role="Bind"}]};
            var r=Tree(Call("SearchEngine","Run",cfg,"H")); var q=r.GetProperty("Query");
            Check(q.GetProperty("Found").GetBoolean(),"direct housing found");
            Check(q.GetProperty("DispenseStation").GetString()==cfg.Folders[1].Key,"latest across stations");
            Check(q.GetProperty("ThresholdStation").GetString()==cfg.Folders[1].Key,"direct threshold");
            Check(!Tree(Call("SearchEngine","Run",cfg,"HH")).GetProperty("Query").GetProperty("Found").GetBoolean(),"exact housing no substring");
        }));
    }
}
