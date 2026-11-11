using FlexExtractor;
static partial class Cases {
 public static void TestOrphanTempIsBoundedAndReused(){var c=Cfg(Fresh());Source(c);var folder=c.DispenseItems[0].SaveFolder;Directory.CreateDirectory(folder);var temp=Path.Combine(folder,".flex-copy-v2.tmp");File.WriteAllText(temp,"orphaned half copy");Scan(c);Program.Check(!File.Exists(temp),"orphan temporary file left behind");Program.Check(Directory.GetFiles(folder,"*.tmp").Length==0,"temporary files accumulate");}
 public static void TestPendingManifestCrashRecovery(){var c=Cfg(Fresh());Source(c);Scan(c);var folder=c.DispenseItems[0].SaveFolder;var m=Ownership.Load(folder);m.Files[0].Committed=false;m.Save(folder);Scan(c,"purge");Program.Check(Ownership.Load(folder).Files[0].Committed,"pending published file never recovered");}
}
