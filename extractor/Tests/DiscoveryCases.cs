using FlexExtractor;
static partial class Cases {
 public static void TestDiscoverHierarchyWithoutCopy(){var c=Cfg(Fresh());Source(c,"partProcessed/deep/Request.xml");var result=(ScanResult)Scan(c,"discover");Program.Check(result.Folders.SequenceEqual(new[]{"partProcessed","partProcessed/deep"}),"hierarchy discovery missing");Program.Check(!Directory.Exists(c.DispenseItems[0].SaveFolder),"discovery performed copy");}
}
