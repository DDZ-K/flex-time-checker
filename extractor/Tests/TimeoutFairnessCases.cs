using FlexExtractor;
static partial class Cases {
 public static void TestInterruptedBatchCursorAdvancesBeforeBlockedOpen(){var c=Cfg(Fresh());c.SizeStableChecks=8;c.SizeStableIntervalMs=1000;c.WorkerTimeoutSec=2;var source=Source(c);using(var owner=new OwnerLease()){var cursor=Path.Combine(MachineState.Root,"cursor.txt");Durable.Write(cursor,"",32768);var result=WorkerRunner.RunAsync(owner,c,"scan",DateTime.UtcNow,CancellationToken.None).GetAwaiter().GetResult();Program.Check(result.Failed>0,"locked source failure invisible");Program.Check(Path.GetFullPath(Durable.Read(cursor,32768))==Path.GetFullPath(source),"cursor does not advance before failed IO");}}
}
