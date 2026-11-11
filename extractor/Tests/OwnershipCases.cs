using FlexExtractor;
static partial class Cases {
 public static void TestOwnershipPurgeOfflinePreservesForeign(){var c=Cfg(Fresh());Source(c);Scan(c);var folder=c.DispenseItems[0].SaveFolder;var foreign=Path.Combine(folder,"Request_foreign.xml");File.WriteAllText(foreign,"<foreign/>");File.SetLastWriteTimeUtc(foreign,DateTime.UtcNow.AddDays(-8));Directory.Delete(c.WatchRoot,true);var t=typeof(Config).Assembly.GetType("FlexExtractor.Ownership");Program.Check(t!=null,"persistent ownership manifest missing");Invoke(t!,"Purge",null,folder,1,DateTime.UtcNow.AddDays(2),new ScanResult());Program.Check(File.Exists(foreign),"foreign file deleted");Program.Check(Directory.GetFiles(folder,"Request*.xml").Length==1,"owned expired not purged offline");}
}
