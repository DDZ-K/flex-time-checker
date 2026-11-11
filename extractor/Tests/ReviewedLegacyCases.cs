using FlexExtractor;
using System.Text.Json;
static partial class Cases {
 public static void TestReviewLegacyNullItemsNormalizeAndFallback(){foreach(var fields in new[]{"\"DispenseItems\":null,\"BindItems\":[]","\"DispenseItems\":[],\"BindItems\":null","\"DispenseItems\":null,\"BindItems\":null"}){var cfg=Config.Parse("{\"WatchRoot\":\"C:\\\\watch\",\"OutputRoot\":\"C:\\\\cache\",\"DispenseStations\":[\"31\"],\"BindStations\":[\"41\"],"+fields+"}",true,"C:\\old");cfg.Validate();Program.Check(cfg.DispenseItems.Single().Token=="31"&&cfg.BindItems.Single().Token=="41","legacy null list fallback lost");}}
 public static void TestReviewLegacyNullItemsKeepExplicitOtherRole(){var root=Fresh();var cfg=Cfg(root);cfg.BindItems=[cfg.DispenseItems.Single()];cfg.DispenseItems=null!;var imported=Config.Parse(JsonSerializer.Serialize(cfg,Config.Json),true,root);Program.Check(imported.DispenseItems.Count==0&&imported.BindItems.Count==1,"explicit opposite-role items lost");}
}
