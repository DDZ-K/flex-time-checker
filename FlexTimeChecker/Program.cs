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
                    Role = Roles.Bind
                }
            ]
        };
        cfg.Normalize();

        var byHousing = RequestXmlSearch.Find(housing, cfg);
        var byMaterial = RequestXmlSearch.Find(material, cfg);
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
                 && !unknown.Found
                 && other.Found
                 && other.HousingCode == otherHousing
                 && !materialNoDispense.Found
                 && (materialNoDispense.Error ?? "").Contains("点胶站未找到")
                 && waitOk;

        var sb = new StringBuilder();
        sb.AppendLine($"housingFound={byHousing.Found}");
        sb.AppendLine($"housingCode={byHousing.HousingCode}");
        sb.AppendLine($"housingTime={byHousing.DispenseTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"materialFound={byMaterial.Found}");
        sb.AppendLine($"materialToHousing={byMaterial.HousingCode}");
        sb.AppendLine($"materialTime={byMaterial.DispenseTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"unknownFound={unknown.Found}");
        sb.AppendLine($"otherFound={other.Found}");
        sb.AppendLine($"materialNoDispenseFound={materialNoDispense.Found}");
        sb.AppendLine($"materialNoDispenseError={materialNoDispense.Error}");
        sb.AppendLine($"waitOk={waitOk}");
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
