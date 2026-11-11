using System.Diagnostics;
using System.Globalization;
using FlexTimeChecker.Core;
namespace FlexTimeChecker;
internal static class UiSoak
{
    public static async Task<int> RunAsync(int seconds,string reportDirectory)
    {
        var root=Path.Combine(reportDirectory,"soak-data-"+Guid.NewGuid().ToString("N")); var dispense=Path.Combine(root,"dispense"); Directory.CreateDirectory(dispense);
        using var stop=new CancellationTokenSource(); var writes=0; var deletes=0; var changes=0; Task? writer=null;
        var samples=new List<(double Seconds,int Cycles,int Handles,long Private,long Working,long Managed,int LogLines,int LogChars,int Jobs)>();
        var sw=Stopwatch.StartNew(); var cycles=0;
        try
        {
            string Xml(string h)=>$"<root><header timeStamp=\"{DateTimeOffset.Now.AddMinutes(-1):O}\"/><partProcessed identifier=\"{h}\"/></root>";
            File.WriteAllText(Path.Combine(dispense,"Request_stable.xml"),Xml("H-STABLE"));
            for(var i=0;i<150;i++) File.WriteAllText(Path.Combine(dispense,$"Request_seed_{i}.xml"),Xml("H-"+i));
            var cfg=new AppConfig{Folders=[new(){Name="模拟点胶站（非现场）",MessageFolder=dispense}],WorkerTimeoutSeconds=10};
            using var form=new MainForm(cfg,Path.Combine(root,"state"),false); form.WindowState=FormWindowState.Normal; form.ClientSize=new(1280,900); form.Show();
            var log=(TextBox)UiSelfTests.Walk(form).Single(c=>c.Name=="RecentLog");
            writer=Task.Run(async()=>{
                var i=0;
                while(!stop.IsCancellationRequested)
                {
                    var path=Path.Combine(dispense,$"Request_change_{i%80}.xml");
                    File.WriteAllText(path,i%13==0?"<root>":Xml("DYNAMIC-"+(i%80))); Interlocked.Increment(ref writes); Interlocked.Increment(ref changes);
                    if(i%3==0) { var old=Path.Combine(dispense,$"Request_change_{(i+41)%80}.xml"); if(File.Exists(old)) { File.Delete(old); Interlocked.Increment(ref deletes); } }
                    if(i%11==0) { var dir=Path.Combine(dispense,"transient"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir,"Request_child.xml"),Xml("CHILD")); Directory.Delete(dir,true); Interlocked.Increment(ref deletes); }
                    i++; try { await Task.Delay(35,stop.Token); } catch(OperationCanceledException) { break; }
                }
            });
            void Sample()
            {
                using var process=Process.GetCurrentProcess(); process.Refresh();
                var jobs=Path.Combine(root,"state","jobs");
                samples.Add((sw.Elapsed.TotalSeconds,cycles,process.HandleCount,process.PrivateMemorySize64,process.WorkingSet64,GC.GetTotalMemory(false),log.Lines.Length,log.TextLength,Directory.Exists(jobs)?Directory.EnumerateDirectories(jobs).Count():0));
            }
            Sample(); var next=10d;
            while(sw.Elapsed.TotalSeconds<seconds)
            {
                await form.QueryAsync("H-STABLE"); cycles++;
                if(log.TextLength>65536||log.Lines.Length>200) throw new Exception("UI log exceeded bound");
                if(sw.Elapsed.TotalSeconds>=next) { Sample(); next+=10; }
                await Task.Delay(30);
            }
            stop.Cancel(); await writer;
            foreach(var p in Directory.EnumerateFiles(dispense,"Request_change_*.xml")) File.Delete(p);
            await form.QueryAsync("H-STABLE");
            if(UiSelfTests.Walk(form).OfType<VerdictCard>().First(c=>c.Name=="VerdictDemolish").VerdictText!="可以") throw new Exception("final stable scan did not recover");
            Sample(); form.Close();
            var min=samples.Min(s=>s.Handles); var max=samples.Max(s=>s.Handles);
            var growth=samples[^1].Private-samples[0].Private;
            if(max-min>100||growth>134217728||samples[^1].Jobs!=0) throw new Exception("resource envelope exceeded");
            Console.WriteLine($"PASS UI SOAK elapsedSeconds={sw.Elapsed.TotalSeconds:F3} cycles={cycles} writes={writes} changes={changes} deletes={deletes} handlesMin={min} handlesMax={max} privateGrowthBytes={growth} finalLogLines={samples[^1].LogLines} finalLogChars={samples[^1].LogChars} remainingJobs={samples[^1].Jobs}");
            return 0;
        }
        catch(Exception ex) { Console.WriteLine("FAIL UI SOAK "+ex); return 2; }
        finally
        {
            stop.Cancel(); if(writer is not null) await writer;
            var rows=new List<string>{"elapsedSeconds,cycles,handles,privateBytes,workingBytes,managedBytes,logLines,logChars,remainingJobs"};
            rows.AddRange(samples.Select(s=>string.Join(",",s.Seconds.ToString("F3",CultureInfo.InvariantCulture),s.Cycles,s.Handles,s.Private,s.Working,s.Managed,s.LogLines,s.LogChars,s.Jobs)));
            File.WriteAllLines(Path.Combine(reportDirectory,"soak-metrics.csv"),rows);
            Directory.Delete(root,true);
        }
    }
}
