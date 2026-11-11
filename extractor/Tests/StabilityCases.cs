using FlexExtractor;
using System.Diagnostics;
static partial class Cases {
 public static void TestStableChecksActuallyWait(){var c=Cfg(Fresh());c.SizeStableChecks=2;c.SizeStableIntervalMs=100;Source(c);var watch=Stopwatch.StartNew();Scan(c);Program.Check(watch.ElapsedMilliseconds>=190,"stable checks ignored");}
}
