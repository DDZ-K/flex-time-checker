using FlexTimeChecker.Core;
using System.Reflection;
namespace FlexTimeChecker.Tests;
public static partial class SelfTests
{
    static readonly List<(string Name, Action Test)> Tests = [];
    public static int Run(string? only = null)
    {
        Tests.Clear(); Register(); var failed = 0;
        foreach (var (name, test) in Tests.Where(t => only is null || t.Name.Contains(only, StringComparison.OrdinalIgnoreCase)))
        { try { test(); Console.WriteLine("PASS " + name); } catch(Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.GetBaseException().Message); } }
        Console.WriteLine($"RESULT total={Tests.Count(t=>only is null || t.Name.Contains(only,StringComparison.OrdinalIgnoreCase))} failed={failed}"); return failed == 0 ? 0 : 2;
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static object Call(string type, string method, params object?[] args)
    {
        var t = typeof(WaitRules).Assembly.GetType("FlexTimeChecker.Core." + type);
        Check(t != null, type + " behavior missing");
        return t!.GetMethod(method)!.Invoke(null, args)!;
    }
    static void Throws(Action action) { try { action(); } catch(Exception ex) when(ex.GetBaseException() is System.Text.Json.JsonException or InvalidDataException or ArgumentException) { return; } throw new Exception("expected validation rejection"); }
    static void Register()
    {
        RegisterXml(); RegisterSearch(); RegisterRework(); RegisterWorker();
        foreach(var (label,fields) in new[]{("string minutes","\"MessageFolder\":\"C:\\\\synthetic\",\"DemolishMinutes\":\"oops\""),("overflow minutes","\"MessageFolder\":\"C:\\\\synthetic\",\"DemolishMinutes\":9999999999999999"),("numeric path","\"MessageFolder\":123")})
            Tests.Add(("legacy malformed top-level "+label+" produces handled validation rejection",()=>Throws(()=>ConfigStore.Parse("{"+fields+"}",true))));
        Tests.Add(("legacy comments and trailing commas accepted only by explicit legacy entry",()=>{
            const string legacy="{/*old config*/\"Folders\":[{\"Name\":\"D\",\"MessageFolder\":\"C:\\\\synthetic\",\"Role\":\"Dispense\",},],}";
            Check(ConfigStore.Parse(legacy,true).Folders.Count==1,"explicit old config allows comments and trailing commas");
            Throws(()=>ConfigStore.Parse(legacy,false));
            foreach(var explicitImport in new[]{false,true}) Throws(()=>ConfigStore.Parse(legacy.Replace("/*old config*/","/*current*/\"SchemaVersion\":1,"),explicitImport));
        }));
        Tests.Add(("legacy null or empty folders fall back to explicit top-level folder",()=>{
            foreach(var folders in new[]{"null","[]"})
            {
                var cfg=ConfigStore.Parse("{\"Folders\":"+folders+",\"MessageFolder\":\"C:\\\\synthetic\",\"DemolishMinutes\":31}",true);
                Check(cfg.Folders.Count==1&&cfg.Folders[0].MessageFolder=="C:\\synthetic"&&cfg.Folders[0].DemolishMinutes==31,"legacy null/empty Folders fallback priority");
            }
            var preferred=ConfigStore.Parse("{\"Folders\":[{\"MessageFolder\":\"C:\\\\station\"}],\"MessageFolder\":\"C:\\\\fallback\"}",true);
            Check(preferred.Folders.Single().MessageFolder=="C:\\station","nonempty Folders takes precedence over top-level");
            Throws(()=>ConfigStore.Parse("{\"Folders\":null}",true));
        }));
        Tests.Add(("legacy role whitespace is trimmed before alias normalization",()=>{
            foreach(var (raw,expected) in new[]{(" Bind ","Bind"),(" Assembly ","Bind"),(" Dispense ","Dispense")})
            {
                var cfg=ConfigStore.Parse("{\"Folders\":[{\"MessageFolder\":\"C:\\\\synthetic\",\"Role\":\""+raw+"\"}]}",true);
                Check(cfg.Folders.Single().Role==expected,"legacy role trim preserves role");
            }
        }));
        Tests.Add(("legacy null role and null station are rejected as invalid configuration",()=>{
            foreach(var folders in new[]{"[null]","[{\"Name\":\"D\",\"MessageFolder\":\"C:\\\\synthetic\",\"Role\":null}]"}) Throws(()=>ConfigStore.Parse("{\"Folders\":"+folders+"}",true));
        }));
        Tests.Add(("config duplicate nested fields cannot silently override minutes",()=>{
            var text="{\"SchemaVersion\":1,\"Folders\":[{\"Name\":\"D\",\"MessageFolder\":\"C:\\\\synthetic\",\"DemolishMinutes\":10,\"DemolishMinutes\":120}]}";
            Throws(()=>ConfigStore.Parse(text,false));
        }));
        Tests.Add(("strict wait boundaries and future", () => {
            foreach (var (m,d,c) in new[] {(29d,true,false),(30d,false,false),(240d,false,false),(241d,false,true),(-1d,false,false)})
            { var v=WaitRules.Evaluate(TimeSpan.FromMinutes(m),30,240); Check(v.CanDemolish==d && v.CanClear==c,"boundary "+m); if(m<0) Check(v.Error!=null,"future explanation"); }
        }));
        Tests.Add(("config explicit legacy import and invalid preservation", () => {
            const string legacy="{\"Folders\":[{\"Name\":\"D1\",\"Role\":\"Dispense\",\"MessageFolder\":\"C:\\\\synthetic\",\"DemolishMinutes\":30,\"ClearGlueMinutes\":240}]}";
            Throws(()=>Call("ConfigStore","Parse",legacy,false));
            var imported=Call("ConfigStore","Parse",legacy,true);
            Check(imported!=null,"legacy explicit import");
            foreach(var bad in new[]{"{}","{","{\"SchemaVersion\":99,\"Folders\":[]}","{\"SchemaVersion\":1,\"Unexpected\":1,\"Folders\":[]}"}) Throws(()=>Call("ConfigStore","Parse",bad,false));
        }));
    }
}
