using FlexTimeChecker.Core;
namespace FlexTimeChecker;
internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        if(args.Contains("--worker-hang")) { Thread.Sleep(Timeout.Infinite); return 9; }
        if(args.Contains("--worker"))
        {
            var dir=args[Array.IndexOf(args,"--worker")+1];
            if(args.Contains("--worker-fault")) { File.WriteAllText(Path.Combine(dir,"error.txt"),"synthetic-worker-fault"); return 2; }
            var result=WorkerHost.Run(dir);
            if(args.Contains("--worker-forge")&&result==0)
            {
                var file=Path.Combine(dir,"reply.json"); var reply=System.Text.Json.JsonSerializer.Deserialize<WorkerReply>(File.ReadAllText(file),ConfigStore.Json)!;
                File.WriteAllText(file,System.Text.Json.JsonSerializer.Serialize(reply with{Generation=-999},ConfigStore.Json));
            }
            return result;
        }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if(args.Contains("--ui-test")||args.Contains("--self-test")||args.Contains("--soak-seconds"))
        {
            var output=args.Contains("--report")?args[Array.IndexOf(args,"--report")+1]:Path.Combine(AppContext.BaseDirectory,"selftest-result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!); using var writer=new StringWriter(); Console.SetOut(writer);
            var result=0;
            if(args.Contains("--ui-test")||args.Contains("--soak-seconds"))
            {
                using var host=new Form{ShowInTaskbar=false,WindowState=FormWindowState.Minimized};
                host.Shown+=async(_,_)=>{
                    result=args.Contains("--soak-seconds")?await UiSoak.RunAsync(Math.Clamp(int.Parse(args[Array.IndexOf(args,"--soak-seconds")+1]),5,86400),Path.GetDirectoryName(Path.GetFullPath(output))!):UiSelfTests.Run(Path.GetDirectoryName(Path.GetFullPath(output))!);
                    host.Close();
                };
                Application.Run(host);
            }
            else result=Tests.SelfTests.Run();
            File.WriteAllText(output,writer.ToString()); return result;
        }
        try
        {
            var store=args.Contains("--data-dir")?args[Array.IndexOf(args,"--data-dir")+1]:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlexTimeCheckerStable");
            store=Path.GetFullPath(store);
            if(store.StartsWith("\\\\")||new DriveInfo(Path.GetPathRoot(store)!).DriveType==DriveType.Network) throw new InvalidDataException("配置/返工/任务目录必须位于本机磁盘");
            var executable=Path.GetFullPath(Environment.ProcessPath!);
            if(executable.StartsWith("\\\\")||new DriveInfo(Path.GetPathRoot(executable)!).DriveType==DriveType.Network) throw new InvalidDataException("请把程序复制到本机磁盘后运行");
            Directory.CreateDirectory(store);
            using var instance=TryLockStore(store);
            if(instance is null) { if(!args.Contains("--instance-probe")) MessageBox.Show("此数据目录的检查器已在运行（可能位于其他 Windows 会话），请使用已有窗口。","单实例"); return 10; }
            var path=Path.Combine(store,"config.json"); var cfg=new AppConfig(); var unsafeConfig=false;
            if(File.Exists(path))
            {
                try { cfg=ConfigStore.Load(path); }
                catch(Exception ex) when(ex is System.Text.Json.JsonException or InvalidDataException or ArgumentException||SearchEngine.IsExpectedIo(ex))
                { unsafeConfig=true; MessageBox.Show("现有配置未知、损坏或不可读，已保留原文件，未自动覆盖。请打开配置，显式导入或新建。","配置未加载",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
            }
            else if(File.Exists(Path.Combine(store,"rework.json"))) unsafeConfig=true;
            using var form=new MainForm(cfg,store){NeedsConfigRecovery=unsafeConfig};
            using var smoke=new System.Windows.Forms.Timer();
            if(args.Contains("--smoke-seconds")) { smoke.Interval=Math.Clamp(int.Parse(args[Array.IndexOf(args,"--smoke-seconds")+1]),1,120)*1000; smoke.Tick+=(_,_)=>{smoke.Stop();form.Close();}; smoke.Start(); }
            Application.Run(form); return 0;
        }
        catch(Exception ex) { MessageBox.Show("启动失败 ("+ex.GetType().Name+")："+ex.Message,"未启动",MessageBoxButtons.OK,MessageBoxIcon.Error); return 2; }
    }
    static FileStream? TryLockStore(string store)
    {
        // Kernel file sharing is machine-wide, unlike a Local\\ named mutex.
        // Never unlink this file: deleting/recreating a lock introduces an ownership race.
        try { return new FileStream(Path.Combine(store,".instance.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
        catch(IOException ex) when((ex.HResult&0xffff) is 32 or 33) { return null; }
    }
}
