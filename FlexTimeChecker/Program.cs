using System.Text;

namespace FlexTimeChecker;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = RunSelfTest();
            return;
        }

        if (args.Any(a => string.Equals(a, "--demo-bind-editor", StringComparison.OrdinalIgnoreCase)))
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FolderProfileEditor(new FolderProfile
            {
                Name = "组装61",
                Role = Roles.Bind,
                MessageFolder = @"E:\example\bind",
                DemolishMinutes = 30,
                ClearGlueMinutes = 240
            }));
            return;
        }

        if (args.Any(a => string.Equals(a, "--demo-config", StringComparison.OrdinalIgnoreCase)))
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FolderProfilesDialog(
            [
                new FolderProfile { Name = "点胶31", Role = Roles.Dispense, MessageFolder = @"E:\example\dispense", DemolishMinutes = 30, ClearGlueMinutes = 240 },
                new FolderProfile { Name = "组装61", Role = Roles.Bind, MessageFolder = @"E:\example\bind", DemolishMinutes = 30, ClearGlueMinutes = 240 }
            ]));
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        AntdUI.Localization.DefaultLanguage = "zh-CN";
        AntdUI.Config.TextRenderingHighQuality = true;
        AntdUI.Config.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        Application.Run(new MainForm());
    }

    private static int RunSelfTest()
    {
        var root = Path.Combine(Path.GetTempPath(), "flex-time-" + Guid.NewGuid().ToString("N"));
        var dispenseDir = Path.Combine(root, "dispense");
        var bindDir = Path.Combine(root, "bind");
        Directory.CreateDirectory(dispenseDir);
        Directory.CreateDirectory(bindDir);

        const string housing = "0097953577001005114350002608218657010372";
        const string material = "69161000053790012608228629503910";
        const string otherHousing = "0097953577001031149940002607108657010229";

        File.WriteAllText(Path.Combine(dispenseDir, "Request_housing.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <root>
              <header timeStamp="2026-08-24T19:06:46.7991675+08:00" />
              <event>
                <partProcessed identifier="0097953577001005114350002608218657010372" />
              </event>
              <body>
                <structArrays>
                  <array name="components">
                    <values>
                      <item matID="GLUA" batch="H122Q1K037" />
                    </values>
                  </array>
                </structArrays>
              </body>
            </root>
            """);

        File.WriteAllText(Path.Combine(bindDir, "Request_bind.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <root>
              <header timeStamp="2026-08-24T11:14:26.3593630+08:00" />
              <event>
                <partProcessed identifier="0097953577001005114350002608218657010372" />
              </event>
              <body>
                <structArrays>
                  <array name="components">
                    <values>
                      <item identifier="69161000053790012608228629503910" typeNo="7517650543" typeVar="0000" class="NA" batch="NA" />
                    </values>
                  </array>
                </structArrays>
              </body>
            </root>
            """);

        File.WriteAllText(Path.Combine(dispenseDir, "Request_other.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <root>
              <header timeStamp="2026-08-24T18:00:00+08:00" />
              <event>
                <partProcessed identifier="0097953577001031149940002607108657010229" />
              </event>
            </root>
            """);

        const string matIdPcba = "69165000175830012608258629503716";
        const string matIdBottom = "066547286570101930033260001";
        const string housingPcba = "071360586570101920033260004";
        const string housingBottom = "059824686570101920033260004";

        File.WriteAllText(Path.Combine(bindDir, "Request_matid_pcba.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <header timeStamp="2026-08-26T19:23:18.8833153+08:00" />
              <event>
                <partProcessed identifier="071360586570101920033260004" />
              </event>
              <body>
                <structArrays>
                  <array name="matDataBComponents">
                    <values>
                      <item matID="69165000175830012608258629503716" typeNo="7517402383" batch="NA" class="Main_PCBA" vendor="Bosch" qty="0" />
                    </values>
                  </array>
                </structArrays>
                <items>
                  <item name="MainPcb_Identifier" value="69165000175830012608258629503716" dataType="8" />
                </items>
              </body>
            </root>
            """);

        File.WriteAllText(Path.Combine(bindDir, "Request_matid_bottom.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <header timeStamp="2026-08-26T19:29:48.1062889+08:00" />
              <event>
                <partProcessed identifier="059824686570101920033260004" />
              </event>
              <body>
                <structArrays>
                  <array name="matDataBComponents">
                    <values>
                      <item matID="97120955S015400019726" typeNo="8657211015" batch="20260420" class="Antenna" vendor="Bosch" qty="1" />
                      <item matID="066547286570101930033260001" typeNo="7517402383" batch="NA" class="Bottom" vendor="Bosch" qty="0" />
                    </values>
                  </array>
                </structArrays>
              </body>
            </root>
            """);

        File.WriteAllText(Path.Combine(dispenseDir, "Request_housing_pcba.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <root>
              <header timeStamp="2026-08-26T18:01:00+08:00" />
              <event>
                <partProcessed identifier="071360586570101920033260004" />
              </event>
            </root>
            """);

        File.WriteAllText(Path.Combine(dispenseDir, "Request_housing_bottom.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <root>
              <header timeStamp="2026-08-26T18:02:00+08:00" />
              <event>
                <partProcessed identifier="059824686570101920033260004" />
              </event>
            </root>
            """);

        var cfg = new AppConfig
        {
            Folders =
            [
                new FolderProfile
                {
                    Name = "点胶31",
                    MessageFolder = dispenseDir,
                    Role = Roles.Dispense,
                    DemolishMinutes = 30,
                    ClearGlueMinutes = 240
                },
                new FolderProfile
                {
                    Name = "组装绑定",
                    MessageFolder = bindDir,
                    Role = Roles.Bind,
                    DemolishMinutes = 15,
                    ClearGlueMinutes = 100
                },
                new FolderProfile
                {
                    Name = "点胶32",
                    MessageFolder = Path.Combine(root, "dispense2"),
                    Role = Roles.Dispense,
                    DemolishMinutes = 30,
                    ClearGlueMinutes = 240
                }
            ]
        };
        cfg.Normalize();

        var dispense2Dir = Path.Combine(root, "dispense2");
        Directory.CreateDirectory(dispense2Dir);
        const string housingA = "HOUSING-A-0001";
        const string housingB = "HOUSING-B-0002";
        static string HousingXml(string code, string stamp) => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <root>
              <header timeStamp="{stamp}" />
              <event>
                <partProcessed identifier="{code}" />
              </event>
            </root>
            """;
        File.WriteAllText(Path.Combine(dispense2Dir, "Request_a.xml"), HousingXml(housingA, "2026-08-26T10:00:00+08:00"));
        File.WriteAllText(Path.Combine(dispense2Dir, "Request_a_dup.xml"), HousingXml(housingA, "2026-08-26T10:01:00+08:00"));
        File.WriteAllText(Path.Combine(dispense2Dir, "Request_b.xml"), HousingXml(housingB, "2026-08-26T10:02:00+08:00"));

        var byHousing = RequestXmlSearch.Find(housing, cfg);
        var byMaterial = RequestXmlSearch.Find(material, cfg);
        var byMatId = RequestXmlSearch.Find(matIdPcba, cfg);
        var byBottom = RequestXmlSearch.Find(matIdBottom, cfg);
        var unknown = RequestXmlSearch.Find("NO_SUCH_CODE", cfg);
        var other = RequestXmlSearch.Find(otherHousing, cfg);

        var bindOnly = new AppConfig
        {
            Folders =
            [
                new FolderProfile { Name = "仅组装", MessageFolder = bindDir, Role = Roles.Bind }
            ]
        };
        bindOnly.Normalize();
        var materialNoDispense = RequestXmlSearch.Find(material, bindOnly);

        var waitOk = WaitRules.CanDemolish(TimeSpan.FromMinutes(29), 30)
                     && !WaitRules.CanDemolish(TimeSpan.FromMinutes(30), 30)
                     && !WaitRules.CanClearGlue(TimeSpan.FromMinutes(240), 240)
                     && WaitRules.CanClearGlue(TimeSpan.FromMinutes(241), 240);

        var ok = byHousing.Found
                 && byHousing.MaterialCode is null
                 && byHousing.HousingCode == housing
                 && byHousing.DispenseTime is DateTime t1
                 && t1.Year == 2026 && t1.Month == 8 && t1.Day == 24 && t1.Hour == 19
                 && byMaterial.Found
                 && byMaterial.MaterialCode == material
                 && byMaterial.HousingCode == housing
                 && byMaterial.DispenseTime is DateTime t2
                 && t2 == t1
                 && byHousing.Profile?.DemolishMinutes == 30
                 && byMaterial.Profile?.Name == "组装绑定"
                 && byMaterial.Profile?.DemolishMinutes == 15
                 && byMaterial.Profile?.ClearGlueMinutes == 100
                 && byMatId.Found
                 && byMatId.MaterialCode == matIdPcba
                 && byMatId.HousingCode == housingPcba
                 && byMatId.DispenseTime is DateTime t3
                 && t3.Year == 2026 && t3.Month == 8 && t3.Day == 26 && t3.Hour == 18 && t3.Minute == 1
                 && byBottom.Found
                 && byBottom.MaterialCode == matIdBottom
                 && byBottom.HousingCode == housingBottom
                 && byBottom.DispenseTime is DateTime t4
                 && t4.Minute == 2
                 && !unknown.Found
                 && other.Found
                 && other.HousingCode == otherHousing
                 && !materialNoDispense.Found
                 && (materialNoDispense.Error ?? "").Contains("点胶站未找到")
                 && waitOk;

        var store = Path.Combine(root, "rework.json");
        var first = ReworkTracker.TryRecord(byHousing, cfg, store);
        var dupHouse = ReworkTracker.TryRecord(byHousing, cfg, store);
        var viaMat = ReworkTracker.TryRecord(byMaterial, cfg, store);
        var pcba = ReworkTracker.TryRecord(byMatId, cfg, store);
        var miss = ReworkTracker.TryRecord(unknown, cfg, store);
        var noDisp = ReworkTracker.TryRecord(materialNoDispense, cfg, store);
        var hitA = RequestXmlSearch.Find(housingA, cfg);
        var recA = ReworkTracker.TryRecord(hitA, cfg, store);
        var recA2 = ReworkTracker.TryRecord(hitA, cfg, store);
        var snap = ReworkTracker.Snapshot(cfg, store);
        var s31 = snap.First(x => x.Name == "点胶31");
        var s32 = snap.First(x => x.Name == "点胶32");
        File.Delete(Path.Combine(dispense2Dir, "Request_a.xml"));
        File.Delete(Path.Combine(dispense2Dir, "Request_a_dup.xml"));
        var snapGone = ReworkTracker.Snapshot(cfg, store);
        var s32Gone = snapGone.First(x => x.Name == "点胶32");
        var s31Gone = snapGone.First(x => x.Name == "点胶31");
        var reworkOk = first && !dupHouse && !viaMat && pcba && !miss && !noDisp
                       && hitA.Found && recA && !recA2
                       && s31.Produced == 4 && s31.Rework == 2
                       && s32.Produced == 2 && s32.Rework == 1
                       && Math.Abs(s31.Rate - 0.5) < 1e-9
                       && Math.Abs(s32.Rate - 0.5) < 1e-9
                       && s32Gone.Produced == 1 && s32Gone.Rework == 0
                       && s31Gone.Produced == 4 && s31Gone.Rework == 2;
        ok = ok && reworkOk;

        var sb = new StringBuilder();
        sb.AppendLine($"housingFound={byHousing.Found}");
        sb.AppendLine($"housingCode={byHousing.HousingCode}");
        sb.AppendLine($"housingTime={byHousing.DispenseTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"materialFound={byMaterial.Found}");
        sb.AppendLine($"materialToHousing={byMaterial.HousingCode}");
        sb.AppendLine($"materialTime={byMaterial.DispenseTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"matIdFound={byMatId.Found}");
        sb.AppendLine($"matIdToHousing={byMatId.HousingCode}");
        sb.AppendLine($"bottomFound={byBottom.Found}");
        sb.AppendLine($"bottomToHousing={byBottom.HousingCode}");
        sb.AppendLine($"unknownFound={unknown.Found}");
        sb.AppendLine($"otherFound={other.Found}");
        sb.AppendLine($"materialNoDispenseFound={materialNoDispense.Found}");
        sb.AppendLine($"materialNoDispenseError={materialNoDispense.Error}");
        sb.AppendLine($"housingMinutes={byHousing.Profile?.DemolishMinutes}/{byHousing.Profile?.ClearGlueMinutes}");
        sb.AppendLine($"materialProfile={byMaterial.Profile?.Name}");
        sb.AppendLine($"materialMinutes={byMaterial.Profile?.DemolishMinutes}/{byMaterial.Profile?.ClearGlueMinutes}");
        sb.AppendLine($"waitOk={waitOk}");
        sb.AppendLine($"reworkOk={reworkOk}");
        sb.AppendLine($"s31={s31.Produced}/{s31.Rework}");
        sb.AppendLine($"s32={s32.Produced}/{s32.Rework}");
        sb.AppendLine($"s32gone={s32Gone.Produced}/{s32Gone.Rework}");
        sb.AppendLine($"ok={ok}");

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

        Console.WriteLine(sb.ToString());
        Console.WriteLine("wrote " + outPath);
        try { Directory.Delete(root, true); } catch { }
        return ok ? 0 : 2;
    }
}
