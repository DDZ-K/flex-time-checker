using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace FlexTimeChecker.Core;
public sealed record WorkerRequest(string JobId,long Generation,string ConfigHash,AppConfig Config,string Code,int ParentPid,long ParentStarted);
public sealed record WorkerReply(string JobId,long Generation,string ConfigHash,ScanResult Scan);
public sealed class WorkerFailureException(string message) : Exception(message);
public static class WorkerRunner
{
    static readonly SemaphoreSlim Gate=new(1,1);
    public static async Task<WorkerReply> ExecuteAsync(string executable,string[] prefix,AppConfig cfg,string code,long generation,string jobRoot,int timeoutMs,CancellationToken cancellation)
    {
        if(!await Gate.WaitAsync(0,cancellation).ConfigureAwait(false)) throw new InvalidOperationException("另一个查询/统计任务正在运行");
        string? job=null; Process? process=null;
        try
        {
            ConfigStore.Validate(cfg);
            var full=Path.GetFullPath(jobRoot);
            if(full.StartsWith("\\\\",StringComparison.Ordinal)||new DriveInfo(Path.GetPathRoot(full)!).DriveType==DriveType.Network) throw new InvalidDataException("任务临时目录必须为本机磁盘");
            Directory.CreateDirectory(full);
            if(Directory.EnumerateFileSystemEntries(full).Take(4).Count()>=4) throw new InvalidDataException("隔离任务目录有 4 个以上残留项，已停止新任务；请退出程序后检查占用并清理 jobs 目录");
            var id=Guid.NewGuid().ToString("N"); job=Path.Combine(full,id); Directory.CreateDirectory(job);
            var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cfg,ConfigStore.Json))));
            using var parent=Process.GetCurrentProcess();
            var request=new WorkerRequest(id,generation,digest,cfg,code,parent.Id,parent.StartTime.ToUniversalTime().Ticks);
            AtomicFile.Write(Path.Combine(job,"request.json"),JsonSerializer.Serialize(request,ConfigStore.Json));
            var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=AppContext.BaseDirectory};
            foreach(var arg in prefix) start.ArgumentList.Add(arg);
            start.ArgumentList.Add("--worker"); start.ArgumentList.Add(job);
            process=Process.Start(start)??throw new IOException("无法启动隔离进程");
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(timeoutMs);
            try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
            catch(OperationCanceledException)
            {
                var pid=process.Id;
                if(!process.HasExited) process.Kill(entireProcessTree:true);
                using var termination=new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await process.WaitForExitAsync(termination.Token).ConfigureAwait(false); }
                catch(OperationCanceledException ex) { throw new IOException("隔离进程未确认终止，必须停止接收任务",ex); }
                if(cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation);
                var error=new TimeoutException("报文查询超时，隔离进程已终止；未接受任何旧结果"); error.Data["Pid"]=pid; throw error;
            }
            if(process.ExitCode!=0)
            {
                var errorPath=Path.Combine(job,"error.txt");
                var diagnostic=File.Exists(errorPath)?ConfigStore.ReadLimited(errorPath,65536):"无故障明细（可能启动/父进程监控/硬退出）";
                throw new WorkerFailureException($"隔离进程失败，退出码 {process.ExitCode}；{diagnostic}");
            }
            var text=ConfigStore.ReadLimited(Path.Combine(job,"reply.json"),33554432);
            var reply=JsonSerializer.Deserialize<WorkerReply>(text,ConfigStore.Json)??throw new InvalidDataException("空 worker 结果");
            if(reply.JobId!=id||reply.Generation!=generation||reply.ConfigHash!=digest) throw new InvalidDataException("拒绝任务/配置 generation 不匹配的结果");
            return reply;
        }
        finally
        {
            // A child whose death cannot be confirmed quarantines this runner.
            // Never free the gate and start another worker beside an orphan.
            var safeToReuse=process is null || process.HasExited;
            try
            {
                if(process is not null && !safeToReuse)
                {
                    process.Kill(entireProcessTree:true);
                    safeToReuse=process.WaitForExit(3000);
                    if(!safeToReuse) throw new WorkerFailureException("无法确认隔离进程终止，任务入口已锁定；请人工排查后重启");
                }
                if(job is not null) { try { Directory.Delete(job,true); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
            }
            finally { process?.Dispose(); if(safeToReuse) Gate.Release(); }
        }
    }
}
