using FlexExtractor;
using System.Diagnostics;
static partial class Cases {
 public static void TestHungWorkerKilledBeforeLeaseReleased(){var t=typeof(Config).Assembly.GetType("FlexExtractor.WorkerRunner");Program.Check(t!=null,"isolated worker supervisor missing");var c=Cfg(Fresh());c.WorkerTimeoutSec=2;using(var lease=new OwnerLease()){var sw=Stopwatch.StartNew();var task=(Task<ScanResult>)Invoke(t!,"RunAsync",null,lease,c,"probe-hang",DateTime.UtcNow,CancellationToken.None)!;var result=task.GetAwaiter().GetResult();Program.Check(result.Failed>0,"timeout not visible");Program.Check(sw.Elapsed.TotalSeconds<9,"worker kill unbounded");Rejected(()=>{using var other=new OwnerLease();});}using var after=new OwnerLease();}
}
