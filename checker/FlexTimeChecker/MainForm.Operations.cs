using FlexTimeChecker.Core;
namespace FlexTimeChecker;
public sealed partial class MainForm
{
    readonly System.Windows.Forms.Timer statisticsTimer=new(){Interval=60000};
    public bool NeedsConfigRecovery {get;set;}
    async Task OpenConfigurationAsync()
    {
        if(busy||closing) return; busy=true;
        try
        {
            using var editor=new ConfigDialog(Config);
            if(editor.ShowDialog(this)!=DialogResult.OK||editor.Result is null) return;
            var cfg=editor.Result;
            var path=Path.Combine(storeDirectory,"config.json");
            var replace=NeedsConfigRecovery && File.Exists(path);
            if(replace && MessageBox.Show(this,"原配置无法加载。是否先保留备份，再显式替换为当前编辑配置？","确认替换损坏/未知配置",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
            await SaveConfigurationAsync(cfg,()=>Task.Run(()=>{
                if(replace) File.Copy(path,Path.Combine(storeDirectory,"config.before-replacement.json"),overwrite:false);
                ConfigStore.Save(path,cfg);
            }));
        }
        finally { busy=false; }
        if(!faulted&&!closing) await QueryAsync("");
    }
    Task SaveConfigurationAsync(AppConfig cfg,Func<Task> persist)
    {
        busy=true;
        active=CompleteConfigurationSaveAsync(cfg,persist);
        return active;
    }
    async Task CompleteConfigurationSaveAsync(AppConfig cfg,Func<Task> persist)
    {
        try
        {
            await persist();
            if(closing||IsDisposed) return;
            Config=cfg; NeedsConfigRecovery=false; generation++; demolish.Apply("待查询"); clear.Apply("待查询"); detail.Text="配置已保存，请重新扫码"; status.Text="配置已保存";
            AddLog($"配置已保存：点胶站 {cfg.Folders.Count(f=>!f.IsBind)} / 组装站 {cfg.Folders.Count(f=>f.IsBind)}");
        }
        catch(Exception ex)
        {
            if(closing||IsDisposed) return;
            MessageBox.Show(this,"保存失败 ("+ex.GetType().Name+")，现有配置未被默默覆盖；如备份已存在，请先人工保管。","未保存",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        finally { busy=false; }
    }
    readonly Queue<string> recent=[];
    CancellationTokenSource? operationCancellation;
    Task active=Task.CompletedTask;
    long generation;
    bool busy,closing,allowClose,faulted;
    void InitializeOperations()
    {
        queryButton.Click+=async(_,_)=>await QueryAsync(input.Text);
        configButton.Click+=async(_,_)=>await OpenConfigurationAsync();
        cancelButton.Click+=(_,_)=>operationCancellation?.Cancel();
        input.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter) { e.SuppressKeyPress=true; await QueryAsync(input.Text); }};
        statisticsTimer.Tick+=async(_,_)=>{ if(!busy&&!faulted&&!closing) await QueryAsync(""); };
        Shown+=async(_,_)=>{ input.Focus(); if(autoRun) { statisticsTimer.Start(); await QueryAsync(""); }};
        FormClosing+=async(_,e)=>{
            if(allowClose) return;
            closing=true; statisticsTimer.Stop(); operationCancellation?.Cancel();
            if(!active.IsCompleted) { e.Cancel=true; Enabled=false; await active; allowClose=true; Close(); }
        };
    }
    public Task QueryAsync(string code)
    {
        if(NeedsConfigRecovery) { status.Text="配置待恢复"; detail.Text="现有配置未加载，请显式导入或确认替换；查询/统计暂停，返工状态保留"; return Task.CompletedTask; }
        if(busy||closing||faulted) return Task.CompletedTask; // Drop duplicate scans; never queue them.
        code=code.Trim(); if(code.Length>512) { AddLog("扫码超过 512 字符，已拒绝"); return Task.CompletedTask; }
        busy=true; queryButton.Enabled=false; configButton.Enabled=false; cancelButton.Enabled=true;
        status.Text=code.Length>0?"搜索中":"统计中";
        if(code.Length>0) { demolish.Apply("搜索中"); clear.Apply("搜索中"); }
        operationCancellation=new(); var snapshot=Config with{Folders=Config.Folders.ToList()};
        var stamp=++generation;
        active=RunOperationAsync(snapshot,code,stamp,operationCancellation.Token);
        return active;
    }
    async Task RunOperationAsync(AppConfig snapshot,string code,long stamp,CancellationToken cancellation)
    {
        try
        {
            var reply=await WorkerRunner.ExecuteAsync(Environment.ProcessPath!,[],snapshot,code,stamp,Path.Combine(storeDirectory,"jobs"),snapshot.WorkerTimeoutSeconds*1000,cancellation);
            if(cancellation.IsCancellationRequested||closing||stamp!=generation||IsDisposed) return;
            var rates=await Task.Run(()=>ReworkStore.Apply(Path.Combine(storeDirectory,"rework.json"),reply.Scan,code.Length>0),cancellation);
            if(cancellation.IsCancellationRequested||closing||stamp!=generation||IsDisposed) return;
            board.Apply(rates);
            foreach(var warning in reply.Scan.Diagnostics) AddLog(warning);
            if(code.Length==0) { status.Text=reply.Scan.Diagnostics.Count==0?"准备就绪":"统计不完整"; if(reply.Scan.Diagnostics.Count>0) AddLog("统计不完整：保留返工记录，圆环不放行任何操作"); return; }
            var q=reply.Scan.Query;
            if(!q.Found)
            {
                status.Text="未找到 / 不完整"; demolish.Apply("未知"); clear.Apply("未知"); detail.Text=q.Error;
                AddLog($"{code}：{q.Error}；扫描 {reply.Scan.Entries} 条目 / {reply.Scan.ElapsedMs}ms"); return;
            }
            var profile=snapshot.Folders.Single(f=>f.Key==q.ThresholdStation);
            var now=DateTimeOffset.Now; var elapsed=now-q.Stamp; var verdict=WaitRules.Evaluate(elapsed,profile.DemolishMinutes,profile.ClearGlueMinutes);
            demolish.Apply(verdict.CanDemolish?"可以":"不可以",verdict.CanDemolish); clear.Apply(verdict.CanClear?"可以":"不可以",verdict.CanClear);
            status.Text=verdict.Error is null?"已命中":"时钟异常";
            detail.Text=verdict.Error??$"{(q.Material is null?"Housing":"物料 → Housing")} {q.Housing}    点胶 {q.Stamp.LocalDateTime:yyyy-MM-dd HH:mm:ss}    已过 {elapsed.TotalMinutes:0.0} 分钟";
            AddLog($"Housing {q.Housing}；阈值站 {profile.Name}；点胶 {q.Stamp.LocalDateTime:yyyy-MM-dd HH:mm:ss}；拆除 {(verdict.CanDemolish?"可以":"不可以")} / 清胶 {(verdict.CanClear?"可以":"不可以")}；{reply.Scan.ElapsedMs}ms"+(verdict.Error is null?"":"；"+verdict.Error));
        }
        catch(OperationCanceledException) { if(!IsDisposed&&!closing) { status.Text="已取消"; demolish.Apply("未知"); clear.Apply("未知"); AddLog("任务已取消，隔离进程已终止"); } }
        catch(Exception ex)
        {
            if(IsDisposed||closing) return;
            var expected=SearchEngine.IsExpectedIo(ex)||ex is TimeoutException or System.Text.Json.JsonException;
            status.Text=expected?"任务失败":"内部错误"; demolish.Apply("未知"); clear.Apply("未知");
            detail.Text=ex is TimeoutException?ex.Message:$"任务失败 ({ex.GetType().Name})，未放行；请检查配置/报文/返工状态";
            AddLog(detail.Text);
            if(!expected)
            {
                faulted=true; statisticsTimer.Stop(); AddLog("内部错误已停止接收任务，请保留故障文件并重启排查");
                try { await Task.Run(()=>AtomicFile.Write(Path.Combine(storeDirectory,"last-fault.txt"),ex.ToString()[..Math.Min(ex.ToString().Length,65536)])); }
                catch(Exception writeError) when(SearchEngine.IsExpectedIo(writeError)) { AddLog("故障文件写入失败"); }
            }
        }
        finally
        {
            operationCancellation?.Dispose(); operationCancellation=null; busy=false;
            if(!IsDisposed&&!closing) { queryButton.Enabled=!faulted; configButton.Enabled=true; cancelButton.Enabled=false; if(code.Length>0) { input.SelectAll(); input.Focus(); } }
        }
    }
    void AddLog(string message)
    {
        message=message.Replace('\r',' ').Replace('\n',' ');
        recent.Enqueue($"[{DateTime.Now:HH:mm:ss}] {message[..Math.Min(message.Length,300)]}");
        while(recent.Count>200) recent.Dequeue();
        log.Text=string.Join(Environment.NewLine,recent); log.SelectionStart=log.TextLength; log.ScrollToCaret();
    }
    void DisposeOperations() { statisticsTimer.Dispose(); operationCancellation?.Cancel(); }
}
