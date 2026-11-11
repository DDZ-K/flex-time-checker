using System.Diagnostics;
using FlexTimeChecker.Core;
namespace FlexTimeChecker.Tests;
public static partial class SelfTests
{
    static object AwaitCall(string method,params object?[] args)
    {
        var task=(Task)Call("WorkerRunner",method,args); task.GetAwaiter().GetResult(); return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }
    static void RegisterWorker()
    {
        Tests.Add(("worker abandoned job directories have a hard cap",()=>{
            using var b=new Sandbox(); for(var i=0;i<4;i++) Directory.CreateDirectory(Path.Combine(b.Root,"old-"+i));
            Throws(()=>AwaitCall("ExecuteAsync",Environment.ProcessPath!,new[]{"--worker-hang"},new AppConfig(),"H",1L,b.Root,350,CancellationToken.None));
            Check(Directory.EnumerateDirectories(b.Root).Count()==4,"job backlog must not grow");
        }));
        Tests.Add(("worker failure preserves bounded diagnostic before job cleanup",()=>{
            using var b=new Sandbox();
            try { AwaitCall("ExecuteAsync",Environment.ProcessPath!,new[]{"--worker-fault"},new AppConfig(),"H",1L,b.Root,3000,CancellationToken.None); }
            catch(Exception ex) when(ex.GetBaseException().GetType().Name=="WorkerFailureException")
            { Check(ex.GetBaseException().Message.Contains("synthetic-worker-fault"),"diagnostic retained"); return; }
            throw new Exception("expected distinguishable worker failure with diagnostic");
        }));
        Tests.Add(("worker real query uses immutable request generation and rejects forged reply",()=>{
            using var b=new Sandbox(); b.Put("d/Request_ok.xml",Xml("H"));
            var cfg=new AppConfig{Folders=[new(){MessageFolder=Path.Combine(b.Root,"d")}]};
            var reply=(WorkerReply)AwaitCall("ExecuteAsync",Environment.ProcessPath!,Array.Empty<string>(),cfg,"H",17L,Path.Combine(b.Root,"jobs"),5000,CancellationToken.None);
            Check(reply.Generation==17&&reply.Scan.Query.Found,"real worker result");
            Throws(()=>AwaitCall("ExecuteAsync",Environment.ProcessPath!,new[]{"--worker-forge"},cfg,"H",18L,Path.Combine(b.Root,"jobs"),5000,CancellationToken.None));
        }));
        Tests.Add(("worker cancellation kills child and rejects concurrent task queue",()=>{
            using var b=new Sandbox(); using var cts=new CancellationTokenSource();
            var task=WorkerRunner.ExecuteAsync(Environment.ProcessPath!,new[]{"--worker-hang"},new(),"",1,b.Root,5000,cts.Token);
            Thread.Sleep(100);
            try { WorkerRunner.ExecuteAsync(Environment.ProcessPath!,[],new(),"",2,b.Root,5000,CancellationToken.None).GetAwaiter().GetResult(); throw new Exception("overlap accepted"); }
            catch(InvalidOperationException) { }
            cts.Cancel();
            try { task.GetAwaiter().GetResult(); throw new Exception("cancel ignored"); } catch(OperationCanceledException) { }
            Check(!Directory.EnumerateDirectories(b.Root).Any(),"cancel cleans child job");
        }));
        Tests.Add(("isolated worker timeout kills real hung child without leftovers",()=>{
            using var b=new Sandbox(); var sw=Stopwatch.StartNew();
            try { AwaitCall("ExecuteAsync",Environment.ProcessPath!,new[]{"--worker-hang"},new AppConfig(),"H",1L,b.Root,350,CancellationToken.None); }
            catch(Exception ex) when(ex.GetBaseException() is TimeoutException)
            { Check(sw.Elapsed.TotalSeconds<5,"timeout kill bounded"); Check(!Directory.EnumerateDirectories(b.Root).Any(),"job cleanup"); return; }
            throw new Exception("expected real process timeout");
        }));
    }
}
