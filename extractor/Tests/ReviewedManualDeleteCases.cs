using FlexExtractor;
static partial class Cases {
 // Existing contract, not a new RED: purge retires missing ownership records, then scan repairs.
 public static void TestReviewManualTargetDeletionRepairedAfterPurge(){var c=Cfg(Fresh());c.SizeStableChecks=0;var source=Source(c);Scan(c);var dest=Directory.GetFiles(c.DispenseItems[0].SaveFolder,"Request*.xml").Single();File.Delete(dest);var before=(ScanResult)Scan(c);Program.Check(before.Failed==1&&!File.Exists(dest),"stale ownership must not silently bypass transaction");Scan(c,"purge");var repaired=(ScanResult)Scan(c);Program.Check(repaired.Copied==1&&File.Exists(dest),"purge then scan did not repair manually deleted output");Program.Check(File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(dest)),"repair changed source bytes");}
}
