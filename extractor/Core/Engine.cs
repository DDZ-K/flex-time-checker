using System.Text.Json;
namespace FlexExtractor;
public sealed class SessionState {
 public string Fingerprint {get;set;}="";public DateTime BaselineUtc {get;set;}
 public static SessionState Open(Config config){var json=JsonSerializer.Serialize(config,Config.Json);var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));var path=Path.Combine(MachineState.Root,"session.json");
  if(File.Exists(path)){var state=JsonSerializer.Deserialize<SessionState>(Durable.Read(path,4096),Config.Json)??throw new InvalidDataException("会话状态无效");if(state.BaselineUtc.Kind!=DateTimeKind.Utc||state.BaselineUtc.Year<2020||state.BaselineUtc>DateTime.UtcNow.AddMinutes(5))throw new InvalidDataException("会话时间无效");if(state.Fingerprint==hash)return state;}
  var fresh=new SessionState{Fingerprint=hash,BaselineUtc=DateTime.UtcNow};Durable.Write(path,JsonSerializer.Serialize(fresh,Config.Json),4096);Durable.Write(Path.Combine(MachineState.Root,"cursor.txt"),"",32768);return fresh;
 }
}
public sealed record EngineSnapshot(bool Running,long Copied,long Skipped,long Failed,long Deleted,string Status){public string RecentFile {get;init;}="";public string RecentStation {get;init;}="";}
public sealed class Engine: IAsyncDisposable {
 readonly object gate=new();readonly Config config;OwnerLease? owner;CancellationTokenSource? stop;Task? loop;BoundedLog? log;EngineSnapshot snapshot=new(false,0,0,0,0,"已停止");bool fatal;
 public DateTime BaselineUtc {get;private set;}
 public Engine(Config config){this.config=Config.Parse(JsonSerializer.Serialize(config,Config.Json));this.config.Validate();}
 public EngineSnapshot Snapshot {get{lock(gate)return snapshot;}}
 public string[] Lines=>log?.Snapshot()??[];
 public void Start(){lock(gate){if(loop!=null)throw new InvalidOperationException("此引擎已启动，不可重复使用");owner=new OwnerLease();try{var session=SessionState.Open(config);BaselineUtc=session.BaselineUtc;log=new BoundedLog(MachineState.Root,config.MaxLogLines);log.Write("开始监控；OnlyNew基准="+BaselineUtc.ToString("O"));stop=new();snapshot=snapshot with{Running=true,Status="监控中"};loop=RunAsync(stop.Token);}catch{owner.Dispose();owner=null;throw;}}}
 async Task RunAsync(CancellationToken token){
  await Task.Yield();string previousError="";DateTime lastErrorLog=DateTime.MinValue;
  try{while(!token.IsCancellationRequested){foreach(var operation in new[]{"purge","scan"}){if(token.IsCancellationRequested)break;var result=await WorkerRunner.RunAsync(owner!,config,operation,BaselineUtc,token).ConfigureAwait(false);var error=string.Join(" | ",result.Messages);lock(gate){snapshot=snapshot with{Copied=snapshot.Copied+result.Copied,Skipped=snapshot.Skipped+result.Skipped,Failed=snapshot.Failed+result.Failed,Deleted=snapshot.Deleted+result.Deleted,RecentFile=result.RecentFile.Length>0?result.RecentFile:snapshot.RecentFile,RecentStation=result.RecentFile.Length>0?result.RecentStation:snapshot.RecentStation,Status=result.Failed>0?"故障/等待重试："+error:"监控中"};}
    if(error.Length>0&&(error!=previousError||DateTime.UtcNow-lastErrorLog>TimeSpan.FromMinutes(1))){log!.Write(error);previousError=error;lastErrorLog=DateTime.UtcNow;}
    if(result.Copied>0||result.Deleted>0)log!.Write($"复制 {result.Copied}，删除过期 {result.Deleted}，失败 {result.Failed}");
   }await Task.Delay(TimeSpan.FromSeconds(config.PollIntervalSec),token).ConfigureAwait(false);}
  }catch(OperationCanceledException)when(token.IsCancellationRequested){}
  catch(Exception ex){fatal=true;owner!.Poison();lock(gate)snapshot=snapshot with{Running=false,Status="致命故障，独占锁保留："+ex.Message};try{log!.Write("致命故障 "+ex);}catch(IOException logError){lock(gate)snapshot=snapshot with{Status=snapshot.Status+"；日志失败 "+logError.Message};}throw;}
  finally{if(!fatal){owner!.Dispose();owner=null;lock(gate)snapshot=snapshot with{Running=false,Status="已停止"};}}
 }
 public async Task StopAsync(){Task? pending;lock(gate){stop?.Cancel();pending=loop;if(snapshot.Running)snapshot=snapshot with{Status="停止中：等待工作进程终止"};}if(pending!=null)await pending.ConfigureAwait(false);}
 public async ValueTask DisposeAsync(){await StopAsync().ConfigureAwait(false);stop?.Dispose();}
}
