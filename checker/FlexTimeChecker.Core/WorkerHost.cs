using System.Diagnostics;
using System.Text.Json;
namespace FlexTimeChecker.Core;
public static class WorkerHost
{
    public static int Run(string job)
    {
        try
        {
            var request=JsonSerializer.Deserialize<WorkerRequest>(ConfigStore.ReadLimited(Path.Combine(job,"request.json"),262144),ConfigStore.Json)??throw new InvalidDataException("无效请求");
            using var parent=Process.GetProcessById(request.ParentPid);
            if(parent.StartTime.ToUniversalTime().Ticks!=request.ParentStarted) return 4;
            using var monitorCancellation=new CancellationTokenSource();
            _=WatchParent(parent,monitorCancellation.Token);
            using var watchdog=new Timer(_=>Environment.Exit(4),null,TimeSpan.FromSeconds(request.Config.WorkerTimeoutSeconds+5),Timeout.InfiniteTimeSpan);
            var scan=SearchEngine.Run(request.Config,request.Code);
            var reply=new WorkerReply(request.JobId,request.Generation,request.ConfigHash,scan);
            var text=JsonSerializer.Serialize(reply,ConfigStore.Json);
            if(System.Text.Encoding.UTF8.GetByteCount(text)>33554432) throw new InvalidDataException("结果超过 32 MiB");
            AtomicFile.Write(Path.Combine(job,"reply.json"),text);
            monitorCancellation.Cancel();
            return 0;
        }
        catch(Exception ex)
        {
            // Process boundary: preserve unexpected exceptions as failures, never return normal scan data.
            try { AtomicFile.Write(Path.Combine(job,"error.txt"),ex.ToString()[..Math.Min(ex.ToString().Length,8192)]); }
            catch(IOException) { } catch(UnauthorizedAccessException) { }
            return 2;
        }
    }
    static async Task WatchParent(Process parent,CancellationToken cancellation)
    {
        try { await parent.WaitForExitAsync(cancellation).ConfigureAwait(false); Environment.Exit(4); }
        catch(OperationCanceledException) { }
    }
}
