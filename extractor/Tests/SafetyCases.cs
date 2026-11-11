using FlexExtractor;
using System.Reflection;
static partial class Cases {
 static readonly string TestRoot=Path.Combine(AppContext.BaseDirectory,"selftest-data");
 static readonly string TestMachineRoot=Path.Combine(TestRoot,"machine-"+Guid.NewGuid().ToString("N"));
 static string Fresh(){var d=Path.Combine(TestRoot,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(d);return d;}
 static object? Invoke(Type type,string method,object? target,params object?[] args) {var m=type.GetMethod(method);Program.Check(m!=null,type.Name+"."+method+" missing");try{return m!.Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException!;}}
 static Config Cfg(string root)=>new(){WatchRoot=Path.Combine(root,"watch"),DispenseItems=[new(){Token="31",StationName="点胶31",SaveFolder=Path.Combine(root,"out")}],SizeStableIntervalMs=0};
 static void Rejected(Action a){bool rejected=false;try{a();}catch(Exception e) when((e is System.Reflection.TargetInvocationException?e.InnerException:e) is InvalidDataException or IOException or ArgumentException or System.Text.Json.JsonException){rejected=true;} Program.Check(rejected,"unsafe input accepted");}
 public static void TestPathOverlapValidation(){var c=Cfg(Fresh());c.DispenseItems[0].SaveFolder=Path.Combine(c.WatchRoot,"child");Rejected(()=>Invoke(typeof(Config),"Validate",c));}
}
