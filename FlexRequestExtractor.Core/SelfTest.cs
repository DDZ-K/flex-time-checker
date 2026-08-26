using System.Text;

namespace FlexRequestExtractor;

public static class SelfTest
{
    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "flex-req-extract-" + Guid.NewGuid().ToString("N"));
        var watch = Path.Combine(root, "watch");
        var dispenseA = Path.Combine(root, "save-dispense-31");
        var dispenseB = Path.Combine(root, "save-dispense-21");
        var bindDir = Path.Combine(root, "save-bind-61");
        var today = DateTime.Now.Date;
        var day = Path.Combine(watch, today.ToString("yyyy"), today.ToString("MM"), today.ToString("dd"));
        var st31 = Path.Combine(day, "0031_1_1_1_1", "partReceived");
        var st21 = Path.Combine(day, "0021_1_1_0_0");
        var st61 = Path.Combine(day, "0061_1_1_1_1");
        var st41 = Path.Combine(day, "0041_1_1_1_1");
        Directory.CreateDirectory(st31);
        Directory.CreateDirectory(st21);
        Directory.CreateDirectory(st61);
        Directory.CreateDirectory(st41);
        Directory.CreateDirectory(dispenseA);
        Directory.CreateDirectory(dispenseB);
        Directory.CreateDirectory(bindDir);

        var sessionOld = Path.Combine(st31, "Request_session_old.xml");
        File.WriteAllText(sessionOld, "<header timeStamp=\"2026-08-01T00:00:00+08:00\"/><partProcessed identifier=\"OLD\"/>");
        var threeHoursAgo = DateTime.Now.AddHours(-3);
        File.SetCreationTime(sessionOld, threeHoursAgo);
        File.SetLastWriteTime(sessionOld, threeHoursAgo);

        File.WriteAllText(Path.Combine(st31, "Response_nope.xml"), "<response/>");
        File.WriteAllText(Path.Combine(st31, "note.txt"), "ignore");
        File.WriteAllText(Path.Combine(st41, "Request_other.xml"), "<header timeStamp=\"2026-08-24T19:06:46+08:00\"/><partProcessed identifier=\"OTHER\"/>");

        var cfg = new AppConfig
        {
            WatchRoot = watch,
            OutputRoot = Path.Combine(root, "unused-root"),
            DispenseItems =
            [
                new StationTarget { Station = "31", SaveFolder = dispenseA },
                new StationTarget { Station = "0021", SaveFolder = dispenseB }
            ],
            BindItems =
            [
                new StationTarget { Station = "0061", SaveFolder = bindDir }
            ],
            PollIntervalSec = 1,
            LookbackDays = 2,
            KeepDays = 1,
            OnlyNewAfterStart = true,
            NewFileGraceSec = 1,
            MinFileBytes = 8,
            SizeStableChecks = 0,
            SizeStableIntervalMs = 0
        };
        cfg.Normalize();

        var engine = new ExtractEngine(cfg);
        engine.PrepareSession();

        File.WriteAllText(Path.Combine(st31, "Request_new31.xml"), "<header timeStamp=\"2026-08-24T19:06:46+08:00\"/><partProcessed identifier=\"NEW31\"/>");
        File.WriteAllText(Path.Combine(st21, "Request_new21.xml"), "<header timeStamp=\"2026-08-24T19:06:46+08:00\"/><partProcessed identifier=\"NEW21\"/>");
        File.WriteAllText(Path.Combine(st61, "Request_new61.xml"), "<header timeStamp=\"2026-08-24T19:06:46+08:00\"/><partProcessed identifier=\"NEW61\"/>");

        var first = engine.ScanOnce();
        var dest31 = Path.Combine(dispenseA, "Request_new31.xml");
        var dest21 = Path.Combine(dispenseB, "Request_new21.xml");
        var dest61 = Path.Combine(bindDir, "Request_new61.xml");
        var dest31InBind = Path.Combine(bindDir, "Request_new31.xml");
        var dest31InB = Path.Combine(dispenseB, "Request_new31.xml");
        var destStationFolder = Path.Combine(dispenseA, "0031_1_1_1_1", "Request_new31.xml");
        var destDateFolder = Path.Combine(dispenseA, today.ToString("yyyy"), today.ToString("MM"), today.ToString("dd"), "Request_new31.xml");
        var destSessionOld = Path.Combine(dispenseA, "Request_session_old.xml");
        var destResp = Path.Combine(dispenseA, "Response_nope.xml");
        var destOther = Path.Combine(dispenseA, "Request_other.xml");
        var existed31 = File.Exists(dest31);
        var existed21 = File.Exists(dest21);
        var existed61 = File.Exists(dest61);

        File.Delete(dest31);
        var afterDelete = engine.ScanOnce();

        var staleLocal = Path.Combine(dispenseA, "Request_stale_local.xml");
        File.WriteAllText(staleLocal, "<header timeStamp=\"2026-08-01T00:00:00+08:00\"/><partProcessed identifier=\"STALE\"/>");
        var twoDaysAgo = DateTime.Now.AddHours(-36);
        File.SetCreationTime(staleLocal, twoDaysAgo);
        File.SetLastWriteTime(staleLocal, twoDaysAgo);
        var freshLocal = Path.Combine(dispenseA, "Request_fresh_local.xml");
        File.WriteAllText(freshLocal, "<header timeStamp=\"2026-08-24T19:06:46+08:00\"/><partProcessed identifier=\"FRESH\"/>");

        var staleSource = Path.Combine(st31, "Request_stale_source.xml");
        File.WriteAllText(staleSource, "<header timeStamp=\"2026-08-01T00:00:00+08:00\"/><partProcessed identifier=\"STALESRC\"/>");
        File.SetCreationTime(staleSource, twoDaysAgo);
        File.SetLastWriteTime(staleSource, twoDaysAgo);

        cfg.OnlyNewAfterStart = false;
        var retention = engine.ScanOnce();
        var destStaleSource = Path.Combine(dispenseA, "Request_stale_source.xml");

        var sb = new StringBuilder();
        sb.AppendLine($"copied={first.Copied}");
        sb.AppendLine($"skipped={first.Skipped}");
        sb.AppendLine($"failed={first.Failed}");
        sb.AppendLine($"error={first.Error ?? ""}");
        sb.AppendLine($"hasNew31={existed31}");
        sb.AppendLine($"hasNew21={existed21}");
        sb.AppendLine($"hasNew61={existed61}");
        sb.AppendLine($"has31InBind={File.Exists(dest31InBind)}");
        sb.AppendLine($"has31InOtherDispense={File.Exists(dest31InB)}");
        sb.AppendLine($"hasStationFolder={File.Exists(destStationFolder)}");
        sb.AppendLine($"hasDateFolder={File.Exists(destDateFolder)}");
        sb.AppendLine($"hasSessionOld={File.Exists(destSessionOld)}");
        sb.AppendLine($"hasResponse={File.Exists(destResp)}");
        sb.AppendLine($"hasOther={File.Exists(destOther)}");
        sb.AppendLine($"recopyAfterDelete={afterDelete.CopiedNames.Contains("Request_new31.xml")}");
        sb.AppendLine($"dest31ExistsAfterDelete={File.Exists(dest31)}");
        sb.AppendLine($"purgedStaleLocal={!File.Exists(staleLocal)}");
        sb.AppendLine($"keptFreshLocal={File.Exists(freshLocal)}");
        sb.AppendLine($"skippedStaleSource={!File.Exists(destStaleSource)}");
        sb.AppendLine($"deletedNames={string.Join(",", retention.DeletedNames)}");

        var ok = first.Error is null
                 && first.Copied == 3
                 && existed31
                 && existed21
                 && existed61
                 && first.CopiedNames.Contains("Request_new31.xml")
                 && first.CopiedNames.Contains("Request_new21.xml")
                 && first.CopiedNames.Contains("Request_new61.xml")
                 && !File.Exists(dest31InBind)
                 && !File.Exists(dest31InB)
                 && !File.Exists(destStationFolder)
                 && !File.Exists(destDateFolder)
                 && !File.Exists(destSessionOld)
                 && !File.Exists(destResp)
                 && !File.Exists(destOther)
                 && !afterDelete.CopiedNames.Contains("Request_new31.xml")
                 && !File.Exists(dest31)
                 && !File.Exists(staleLocal)
                 && File.Exists(freshLocal)
                 && !File.Exists(destStaleSource)
                 && retention.DeletedNames.Contains("Request_stale_local.xml");

        sb.AppendLine($"ok={ok}");
        engine.Dispose();

        var outPath = Path.Combine(AppConfig.AppDir, "selftest-result.txt");
        try
        {
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            outPath = Path.Combine(root, "selftest-result.txt");
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        try { Directory.Delete(root, true); } catch { }

        Console.WriteLine(sb.ToString());
        Console.WriteLine("wrote " + outPath);
        return ok ? 0 : 2;
    }
}
