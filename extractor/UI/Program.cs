using FlexExtractor;
namespace FlexExtractor.UI;
internal static class Program { [STAThread] static int Main(string[] args){if(args.Contains("--self-test"))return SelfTests.Run("ui");ApplicationConfiguration.Initialize();AntdUI.Localization.DefaultLanguage="zh-CN";AntdUI.Config.TextRenderingHighQuality=true;if(args.Contains("--preview-config")){Application.Run(new ConfigEditor(new Config()));return 0;}Application.Run(new MainForm());return 0;} }
