using FlexExtractor;
static partial class Cases {
 public static void TestReviewManifestMissingFilesFailsClosed(){var root=Fresh();var path=Path.Combine(root,Ownership.Manifest);File.WriteAllText(path,"{}");Rejected(()=>Ownership.Load(root));Program.Check(File.ReadAllText(path)=="{}","corrupt manifest overwritten");}
 public static void TestReviewManifestExplicitEmptyIsValid(){var root=Fresh();Program.Check(Ownership.Load(root).Files.Count==0,"missing manifest should start empty");File.WriteAllText(Path.Combine(root,Ownership.Manifest),"{\"Files\":[]}");Program.Check(Ownership.Load(root).Files.Count==0,"explicit empty manifest invalid");}
}
