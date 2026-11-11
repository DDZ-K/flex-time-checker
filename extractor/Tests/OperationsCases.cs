using FlexExtractor;
static partial class Cases {
 public static void TestPurgeDoesNotProbeSource(){var c=Cfg(Fresh());Source(c);Scan(c);Directory.Delete(c.WatchRoot,true);var result=(ScanResult)Scan(c,"purge");Program.Check(result.Failed==0,"offline purge failed");}
}
