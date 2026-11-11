using System.Reflection;
using FlexExtractor;
static class Program {
 static int Main(string[] args) { if(args.Length>0&&args[0]=="--probe-parent")return Harness.ProbeParent(args[1]);if(args.Length>0&&args[0]=="--soak")return Harness.Soak(int.Parse(args[1]));if(args.Length>0&&args[0]=="--stress")return Harness.Stress(); int failures=0, count=0; foreach(var m in typeof(Cases).GetMethods(BindingFlags.Public|BindingFlags.Static).Where(x=>x.Name.StartsWith("Test"))) { if(args.Length>0 && !m.Name.Contains(args[0],StringComparison.OrdinalIgnoreCase)) continue; count++; try {m.Invoke(null,null); Console.WriteLine("PASS "+m.Name);} catch(Exception ex) {failures++; Console.WriteLine("FAIL "+m.Name+": "+(ex.InnerException??ex).Message);} } Console.WriteLine($"RESULT total={count} passed={count-failures} failed={failures}"); return failures==0&&count>0?0:1; }
 public static void Check(bool ok,string why) {if(!ok) throw new Exception(why);}
}
static partial class Cases {
 public static void TestConfigRejectsUnknown() { var t=typeof(AssemblyMarker).Assembly.GetType("FlexExtractor.Config"); Program.Check(t!=null,"strict Config API missing"); var m=t!.GetMethod("Parse"); try { m!.Invoke(null,["{\"SchemaVersion\":2,\"Surprise\":true}",false,AppContext.BaseDirectory]); throw new Exception("unknown accepted"); } catch(TargetInvocationException ex) {Program.Check(ex.InnerException is System.Text.Json.JsonException,"wrong error");} }
}
