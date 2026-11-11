using FlexTimeChecker.Core;
namespace FlexTimeChecker;
internal static class UiSelfTests
{
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    internal static IEnumerable<Control> Walk(Control root) { foreach(Control c in root.Controls) { yield return c; foreach(var d in Walk(c)) yield return d; } }
    static void Pump(Task task)
    {
        var until=DateTime.UtcNow.AddSeconds(15);
        while(!task.IsCompleted && DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(5); }
        Check(task.IsCompleted,"UI query completes without blocking"); task.GetAwaiter().GetResult();
    }
    static void RunQueryTest(string reportDirectory)
    {
        var method=typeof(MainForm).GetMethod("QueryAsync"); Check(method!=null,"UI async query behavior missing");
        var data=Path.Combine(reportDirectory,"ui-data-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(data);
        try
        {
            var xml=Path.Combine(data,"Request_h.xml");
            void Write(DateTimeOffset stamp) => File.WriteAllText(xml,$"<root><header timeStamp=\"{stamp:O}\"/><partProcessed identifier=\"H-UI\"/></root>");
            Write(DateTimeOffset.Now.AddMinutes(-1)); var cfg=new AppConfig{Folders=[new(){Name="模拟点胶31",MessageFolder=data,DemolishMinutes=30,ClearGlueMinutes=240}]};
            using var form=new MainForm(cfg,Path.Combine(data,"state"),false); form.WindowState=FormWindowState.Normal; form.Show(); Application.DoEvents();
            var task=(Task)method!.Invoke(form,["H-UI"])!; Check(!task.IsCompleted,"query yields to UI"); Pump(task);
            Check(((VerdictCard)Walk(form).Single(c=>c.Name=="VerdictDemolish")).VerdictText=="可以","query verdict");
            Write(DateTimeOffset.Now.AddMinutes(5)); Pump((Task)method.Invoke(form,["H-UI"])!);
            Check(Walk(form).OfType<VerdictCard>().All(c=>c.VerdictText=="不可以"),"future timestamp fail closed UI");
            var logger=typeof(MainForm).GetMethod("AddLog",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
            for(var i=0;i<1000;i++) logger.Invoke(form,[new string('x',512)]);
            var log=(TextBox)Walk(form).Single(c=>c.Name=="RecentLog"); Check(log.TextLength<=65536&&log.Lines.Length<=201,"bounded UI log");
            var cancelled=form.QueryAsync("H-UI");
            ((CancellationTokenSource)typeof(MainForm).GetField("operationCancellation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(form)!).Cancel();
            Pump(cancelled);
            Check(!Directory.EnumerateDirectories(Path.Combine(data,"state","jobs")).Any(),"cancelled UI query left worker job");
            var closingTask=form.QueryAsync("H-UI"); form.Close(); Pump(closingTask); Application.DoEvents(); Check(form.IsDisposed,"close during query awaits worker termination and disposes form");
            Console.WriteLine("PASS UI real isolated query, future fail-closed, bounded log, cancel and close during query");
        }
        finally { Directory.Delete(data,true); }
    }
    static void RunConfigTest(string reportDirectory)
    {
        var t=typeof(UiSelfTests).Assembly.GetType("FlexTimeChecker.ConfigDialog"); Check(t!=null,"configuration editor behavior missing");
        var cfg=new AppConfig{Folders=[new(){Name="模拟站",MessageFolder=reportDirectory}]};
        using var dialog=(Form)Activator.CreateInstance(t!,[cfg])!; dialog.Show(); Application.DoEvents();
        var grid=Walk(dialog).OfType<DataGridView>().Single(); grid.Rows[0].Cells[0].Value="点胶31"; grid.Rows[0].Cells[3].Value="15";
        var edited=(AppConfig)t!.GetMethod("BuildConfig")!.Invoke(dialog,null)!; Check(edited.Folders[0].Name=="点胶31"&&edited.Folders[0].DemolishMinutes==15,"editable station settings");
        grid.Rows[0].Cells[3].Value="-1";
        try { t.GetMethod("BuildConfig")!.Invoke(dialog,null); throw new Exception("invalid minutes accepted"); } catch(System.Reflection.TargetInvocationException ex) when(ex.InnerException is InvalidDataException) { }
        Check(Walk(dialog).Any(c=>c.Name=="ImportLegacy"),"explicit legacy import command"); dialog.Close();
        var stateRoot=Path.Combine(reportDirectory,"recovery-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stateRoot);
        try
        {
            var state=Path.Combine(stateRoot,"rework.json"); const string preserved="{\"SchemaVersion\":1,\"HousingsByStation\":{\"OLD\":[\"H\"]}}"; File.WriteAllText(state,preserved);
            using var recovery=new MainForm(new(),stateRoot,false){NeedsConfigRecovery=true}; recovery.Show(); Application.DoEvents(); Pump(recovery.QueryAsync(""));
            Check(File.ReadAllText(state)==preserved,"invalid config recovery must not erase persisted rework"); recovery.Close();
        }
        finally {Directory.Delete(stateRoot,true);}
        Console.WriteLine("PASS config editor edit, validation, explicit import and recovery preserves rework");
    }
    static void RunSaveCloseTest(string reportDirectory)
    {
        var method=typeof(MainForm).GetMethod("SaveConfigurationAsync",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        Check(method!=null,"tracked configuration persistence behavior missing");
        var root=Path.Combine(reportDirectory,"save-close-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var cfg=new AppConfig{Folders=[new(){Name="Saved during close",MessageFolder=root}]};
            var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<Task> save=async()=>{await gate.Task; await Task.Run(()=>ConfigStore.Save(Path.Combine(root,"config.json"),cfg));};
            using var form=new MainForm(new(),root,false); form.Show(); Application.DoEvents();
            var task=(Task)method!.Invoke(form,[cfg,save])!;
            Check(!task.IsCompleted,"save paused before real atomic write");
            form.Close(); Application.DoEvents();
            Check(!form.IsDisposed&&!form.Enabled,"close must await pending configuration save");
            gate.SetResult(); Pump(task); Application.DoEvents();
            Check(form.IsDisposed,"form closes after configuration persistence finishes");
            Check(ConfigStore.Load(Path.Combine(root,"config.json")).Folders.Single().Name==cfg.Folders.Single().Name,"configuration committed before exit");
            Check(!Directory.EnumerateFiles(root,"*.tmp").Any(),"configuration save leaves no temporary files");
            Console.WriteLine("PASS UI close awaits pending configuration save; real atomic file read-back and no temp files");
        }
        finally { Directory.Delete(root,true); }
    }
    public static int Run(string reportDirectory)
    {
        try
        {
            var type=typeof(UiSelfTests).Assembly.GetType("FlexTimeChecker.MainForm");
            Check(type is not null,"MainForm behavior missing");
            using var form=(Form)Activator.CreateInstance(type!,[new AppConfig(),reportDirectory,false])!;
            form.WindowState=FormWindowState.Normal; form.ClientSize=new Size(1280,900); form.Show(); Application.DoEvents();
            var controls=Walk(form).ToList(); var input=(TextBox)controls.Single(c=>c.Name=="ScanInput"); var button=controls.Single(c=>c.Name=="QueryButton");
            Check(!input.AutoSize && input.Height==button.Height,"scan TextBox AutoSize=false and query height equality");
            Check(((AntdUI.Button)button).WaveSize==0,"query button painted area must equal scan height");
            Check(controls.Single(c=>c.Name=="LogHost").Height==196,"196-high log");
            Check(controls.Count(c=>c.Name.StartsWith("Verdict"))==2,"two verdict cards");
            Check(!controls.Any(c=>c is Label&&c.Text.Contains("Flex")),"no product heading");
            var board=controls.Single(c=>c.Name=="ReworkBoard"); var apply=board.GetType().GetMethod("Apply")!;
            apply.Invoke(board,[new List<StationRate>{new("D","点胶31",true,10,1)}]); Application.DoEvents();
            var card=Walk(board).Single(c=>c.Name=="Station:D"); var handle=card.Handle;
            apply.Invoke(board,[new List<StationRate>{new("D","点胶31",true,11,2)}]); Application.DoEvents();
            Check(ReferenceEquals(card,Walk(board).Single(c=>c.Name=="Station:D"))&&card.Handle==handle,"donut updated in place without Clear");
            using var bmp=new Bitmap(form.Width,form.Height); form.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size)); bmp.Save(Path.Combine(reportDirectory,"ui-screenshot.png"));
            form.Hide();
            RunSaveCloseTest(reportDirectory); RunQueryTest(reportDirectory); RunConfigTest(reportDirectory);
            Console.WriteLine("PASS UI scan geometry, 196 log, two cards, persistent station handles, screenshot"); return 0;
        }
        catch(Exception ex) { Console.WriteLine("FAIL UI "+ex.GetBaseException()); return 2; }
    }
}
