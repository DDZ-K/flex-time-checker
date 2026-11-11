using FlexTimeChecker.Core;
using System.Globalization;
namespace FlexTimeChecker;
public sealed class ConfigDialog : Form
{
    readonly DataGridView grid=new(){Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Color.White};
    readonly NumericUpDown timeout=new(){Minimum=1,Maximum=120,Width=65};
    readonly Font ownFont=new("Microsoft YaHei UI",10);
    public AppConfig? Result {get;private set;}
    public ConfigDialog(AppConfig cfg)
    {
        Text="站位配置"; StartPosition=FormStartPosition.CenterParent; ClientSize=new(1120,560); MinimumSize=new(900,450); Font=ownFont; BackColor=Color.FromArgb(246,248,251);
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="StationName",HeaderText="站位名称",FillWeight=16});
        grid.Columns.Add(new DataGridViewComboBoxColumn{Name="Role",HeaderText="类型",DataSource=new[]{"Dispense","Bind"},FillWeight=12});
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Folder",HeaderText="报文目录（优先本机；UNC 查询会被进程隔离）",FillWeight=48});
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Demolish",HeaderText="拆除分钟",FillWeight=12}); grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Clear",HeaderText="清胶分钟",FillWeight=12});
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new(8),WrapContents=false};
        AddButton(buttons,"新增点胶站",()=>AddRow(new(){Role="Dispense"})); AddButton(buttons,"新增组装站",()=>AddRow(new(){Role="Bind"}));
        AddButton(buttons,"删除选中",()=>{if(grid.CurrentRow is not null) grid.Rows.Remove(grid.CurrentRow);});
        AddButton(buttons,"选择本机目录",()=>{if(grid.CurrentRow is null) return; using var picker=new FolderBrowserDialog{Description="建议选择提取器的本机保存目录"}; if(picker.ShowDialog(this)==DialogResult.OK) grid.CurrentRow.Cells[2].Value=picker.SelectedPath;});
        var import=AddButton(buttons,"导入 v1.3.6 配置…",ImportLegacy); import.Name="ImportLegacy";
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=55,Padding=new(8),FlowDirection=FlowDirection.RightToLeft};
        AddButton(footer,"取消",()=>{DialogResult=DialogResult.Cancel; Close();});
        AddButton(footer,"保存",()=>{try { Result=BuildConfig(); DialogResult=DialogResult.OK; Close(); } catch(Exception ex) when(ex is InvalidDataException or ArgumentException) { MessageBox.Show(this,ex.Message,"配置未保存",MessageBoxButtons.OK,MessageBoxIcon.Warning); }});
        footer.Controls.Add(timeout); footer.Controls.Add(new Label{Text="查询超时秒（1–120）",AutoSize=true,Margin=new(8)});
        var note=new Label{Dock=DockStyle.Bottom,Height=34,Text="Dispense = 点胶站 / Bind = 组装站；小于拆除分钟可拆除，大于清胶分钟可清胶。导入只预览，点击保存后才生效。",Padding=new(10,5,0,0)};
        Controls.Add(grid); Controls.Add(note); Controls.Add(footer); Controls.Add(buttons); timeout.Value=cfg.WorkerTimeoutSeconds;
        foreach(var f in cfg.Folders) AddRow(f);
        grid.DataError+=(_,e)=>{e.ThrowException=false; MessageBox.Show(this,"类型请选择 Dispense 或 Bind");};
    }
    static AntdUI.Button AddButton(FlowLayoutPanel panel,string text,Action action)
    {
        var b=new AntdUI.Button{Text=text,AutoSizeMode=AntdUI.TAutoSize.Auto,Height=32,WaveSize=0,Radius=6}; b.Click+=(_,_)=>action(); panel.Controls.Add(b); return b;
    }
    void AddRow(FolderProfile f) { if(grid.Rows.Count>=32) { MessageBox.Show(this,"最多 32 个站位"); return; } grid.Rows.Add(f.Name,f.Role,f.MessageFolder,f.DemolishMinutes,f.ClearGlueMinutes); }
    public AppConfig BuildConfig()
    {
        grid.EndEdit(); var folders=new List<FolderProfile>();
        foreach(DataGridViewRow row in grid.Rows)
        {
            string TextAt(int i)=>Convert.ToString(row.Cells[i].Value,CultureInfo.InvariantCulture)?.Trim()??"";
            if(!int.TryParse(TextAt(3),NumberStyles.Integer,CultureInfo.InvariantCulture,out var demolish)||!int.TryParse(TextAt(4),NumberStyles.Integer,CultureInfo.InvariantCulture,out var clear)) throw new InvalidDataException("等待分钟必须为整数");
            folders.Add(new(){Name=TextAt(0),Role=TextAt(1),MessageFolder=TextAt(2),DemolishMinutes=demolish,ClearGlueMinutes=clear});
        }
        var cfg=new AppConfig{Folders=folders,WorkerTimeoutSeconds=(int)timeout.Value}; ConfigStore.Validate(cfg); return cfg;
    }
    void ImportLegacy()
    {
        using var picker=new OpenFileDialog{Filter="JSON 配置|*.json",Title="显式选择旧版配置（不会修改原文件）",CheckFileExists=true};
        if(picker.ShowDialog(this)!=DialogResult.OK) return;
        try
        {
            var path=Path.GetFullPath(picker.FileName);
            if(path.StartsWith("\\\\")||new DriveInfo(Path.GetPathRoot(path)!).DriveType==DriveType.Network) throw new InvalidDataException("请先将旧配置复制到本机磁盘，再导入");
            var imported=ConfigStore.Parse(ConfigStore.ReadLimited(path,262144),true);
            if(MessageBox.Show(this,$"预览导入 {imported.Folders.Count} 个站位？当前编辑内容会被替换；旧文件不变。","确认导入",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
            for(var i=grid.Rows.Count-1;i>=0;i--) grid.Rows.RemoveAt(i);
            foreach(var f in imported.Folders) AddRow(f); timeout.Value=imported.WorkerTimeoutSeconds;
        }
        catch(Exception ex) when(ex is System.Text.Json.JsonException or InvalidDataException or ArgumentException||SearchEngine.IsExpectedIo(ex)) { MessageBox.Show(this,"导入失败："+ex.Message,"旧文件未改动",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if(disposing) ownFont.Dispose(); }
}
