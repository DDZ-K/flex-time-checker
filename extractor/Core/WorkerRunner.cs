using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace FlexExtractor;
public sealed class WorkRequest {
 public Config Config {get;set;}=new(); public string Operation {get;set;}="scan";public string Generation {get;set;}="";public DateTime BaselineUtc {get;set;} public string StateRoot {get;set;}="";public long LeaseHandle {get;set;}
}
public static class WorkerRunner {
 public static async Task<ScanResult> RunAsync(OwnerLease lease,Config config,string operation,DateTime baseline,CancellationToken cancellation){
  var generation=Guid.NewGuid().ToString("N");var result=new ScanResult{Generation=generation};
  using var job=Native.CreateJob();using var process=new Process{StartInfo=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"FlexExtractor.Worker.exe")){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,CreateNoWindow=true}};
  if(!process.Start())throw new IOException("工作进程无法启动");
  bool exited=false;
  try {
   var handle=Native.Attach(job,process,lease);
   var request=new WorkRequest{Config=config,Operation=operation,Generation=generation,BaselineUtc=baseline,StateRoot=MachineState.Root,LeaseHandle=handle};
   var json=JsonSerializer.Serialize(request,Config.Json);if(Encoding.UTF8.GetByteCount(json)>65536)throw new InvalidDataException("工作请求超过64KiB");
   using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(config.WorkerTimeoutSec));
   var output=ReadBoundedAsync(process.StandardOutput,65536,deadline.Token);
   await process.StandardInput.WriteAsync(json.AsMemory(),deadline.Token);process.StandardInput.Close();
   var data=await output;await process.WaitForExitAsync(deadline.Token);exited=true;
   if(process.ExitCode!=0)throw new IOException("工作进程退出码 "+process.ExitCode+" "+data[..Math.Min(1024,data.Length)]);
   var parsed=JsonSerializer.Deserialize<ScanResult>(data,Config.Json)??throw new InvalidDataException("工作结果为空");
   if(parsed.Generation!=generation||parsed.RecentFile==null||parsed.RecentFile.Length>1024||parsed.RecentStation==null||parsed.RecentStation.Length>128||parsed.Messages==null||parsed.Messages.Count>32||parsed.Folders==null||parsed.Folders.Count>128||parsed.Messages.Any(s=>s.Length>1024)||parsed.Folders.Any(s=>s.Length>256))throw new InvalidDataException("工作结果无效或generation不符");return parsed;
  }catch(Exception ex) when(ex is IOException or InvalidDataException or OperationCanceledException or JsonException or System.ComponentModel.Win32Exception){result.Error(ex);return result;}
  finally {if(!exited){if(!Native.TerminateJobObject(job,74)) {if(!process.HasExited)process.Kill(true);}if(!process.WaitForExit(5000)){lease.Poison();throw new IOException("无法确认工作进程已停止；引擎必须保留独占锁，不可再次启动");}}}
 }
 static async Task<string> ReadBoundedAsync(TextReader reader,int max,CancellationToken token){var data=new StringBuilder();var buffer=new char[2048];while(true){var n=await reader.ReadAsync(buffer.AsMemory(),token);if(n==0)return data.ToString();if(data.Length+n>max)throw new InvalidDataException("工作结果超过64KiB");data.Append(buffer,0,n);}}
 public static int WorkerMain(){
  WorkRequest? request=null;try{var text=ReadBoundedAsync(Console.In,65536,CancellationToken.None).GetAwaiter().GetResult();request=JsonSerializer.Deserialize<WorkRequest>(text,Config.Json)??throw new InvalidDataException("空请求");Native.VerifyLease(request.LeaseHandle,request.StateRoot);request.Config.Validate();
   if(request.Operation=="probe-hang")Thread.Sleep(Timeout.Infinite);
   var result=Scanner.Run(request.Config,request.Operation,request.BaselineUtc,Path.Combine(request.StateRoot,"cursor.txt"));result.Generation=request.Generation;var json=JsonSerializer.Serialize(result,Config.Json);if(Encoding.UTF8.GetByteCount(json)>65536)throw new InvalidDataException("结果超过64KiB");Console.Write(json);return 0;
  }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or System.Xml.XmlException){var r=new ScanResult{Generation=request?.Generation??""};r.Error(ex);Console.Write(JsonSerializer.Serialize(r,Config.Json));return 0;}
 }
}
