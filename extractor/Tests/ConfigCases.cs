using FlexExtractor;
static partial class Cases {
 public static void TestMissingSchemaIsNotImplicitlyMigrated(){Rejected(()=>Config.Parse("{\"WatchRoot\":\"C:\\\\watch\",\"DispenseItems\":[]}"));}
 public static void TestLegacyPathsAreNormalizedOnlyOnImport(){var text="""{"WatchRoot":" C:/watch/ ","OutputRoot":"C:/cache/","DispenseStations":[" 31 "],"OnlyNewAfterStart":true}""";var c=Config.Parse(text,true,"C:\\old");c.Validate();Program.Check(c.WatchRoot=="C:\\watch"&&c.DispenseItems[0].Token=="31"&&c.DispenseItems[0].SaveFolder=="C:\\cache\\点胶","legacy path/token normalization missing");}
 public static void TestNullSubFolderIsRejectedCleanly(){var c=Cfg(Fresh());c.DispenseItems[0].SubFolders.Add(null!);Rejected(()=>c.Validate());}
 public static void TestLegacyImportExplicit() {
  var json="""{"WatchRoot":"C:\\watch","OutputRoot":"C:\\cache","Stations":["31"],"KeepDays":3,"LookbackDays":2}""";
  var c=Config.Parse(json,true,"C:\\app"); Program.Check(c.DispenseItems.Count==1 && c.DispenseItems[0].SaveFolder=="C:\\cache\\点胶" && c.LookbackDays==4,"legacy migration semantics");
  bool rejected=false;try{Config.Parse(json);}catch(System.Text.Json.JsonException){rejected=true;}Program.Check(rejected,"implicit migration forbidden");
 }
}
