using System.Diagnostics;
namespace FlexExtractor;
public static class SelfTests {
 public static int Run(string host){using var p=new Process{StartInfo=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"FlexExtractor.Tests.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};if(!p.Start())return 70;var output=p.StandardOutput.ReadToEndAsync();var errors=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(120000)){p.Kill(true);if(!p.WaitForExit(5000))return 74;return 124;}var text=output.GetAwaiter().GetResult()+errors.GetAwaiter().GetResult();Durable.Write(Path.Combine(AppContext.BaseDirectory,"selftest-"+host+".log"),text,1048576);return p.ExitCode;}
}
