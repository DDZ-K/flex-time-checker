using FlexTimeChecker.Core;
using AntdButton=AntdUI.Button;
using AntdPanel=AntdUI.Panel;
namespace FlexTimeChecker;
public sealed partial class MainForm : Form
{
    internal AppConfig Config;
    readonly string storeDirectory;
    readonly List<Font> ownedFonts=[];
    readonly TextBox input,log;
    readonly AntdButton queryButton,configButton,cancelButton;
    readonly Label status,detail,clock;
    readonly VerdictCard demolish,clear;
    readonly ReworkBoard board;
    readonly System.Windows.Forms.Timer clockTimer=new(){Interval=1000};
    readonly bool autoRun;
    public MainForm(AppConfig config,string storeDirectory,bool autoRun=true)
    {
        Config=config; this.storeDirectory=storeDirectory; this.autoRun=autoRun;
        Text="Flex 点胶时间检查"; Name="MainForm"; StartPosition=FormStartPosition.CenterScreen; WindowState=FormWindowState.Maximized;
        MinimumSize=new(1100,820); ClientSize=new(1280,900); BackColor=Color.FromArgb(246,248,251); DoubleBuffered=true;
        Font=MakeFont(10); ForeColor=Color.FromArgb(15,23,42);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Margin=Padding.Empty,Padding=Padding.Empty};
        foreach(var height in new[]{64,76,-1,66,200,196}) root.RowStyles.Add(new(height<0?SizeType.Percent:SizeType.Absolute,height<0?100:height));
        root.ColumnStyles.Add(new(SizeType.Percent,100)); Controls.Add(root);
        var header=new System.Windows.Forms.Panel{Dock=DockStyle.Fill,BackColor=Color.White,Margin=Padding.Empty}; root.Controls.Add(header,0,0);
        configButton=Button("配置",false); configButton.Name="ConfigButton"; configButton.SetBounds(18,12,88,40); header.Controls.Add(configButton);
        var logo=new Label{Text="BOSCH",ForeColor=Color.FromArgb(24,24,27),Font=MakeFont(24,FontStyle.Bold),AutoSize=false,TextAlign=ContentAlignment.MiddleLeft}; logo.SetBounds(126,8,220,46); header.Controls.Add(logo);
        clock=new(){Dock=DockStyle.Right,Width=300,TextAlign=ContentAlignment.MiddleRight,Padding=new(0,0,24,0),Font=MakeFont(12,FontStyle.Bold)}; header.Controls.Add(clock);
        var scan=Surface(); scan.Padding=new(12); scan.Margin=new(18,10,18,8); root.Controls.Add(scan,0,1);
        var scanGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};
        scanGrid.ColumnStyles.Add(new(SizeType.Percent,100)); foreach(var width in new[]{120,88,140}) scanGrid.ColumnStyles.Add(new(SizeType.Absolute,width));
        scanGrid.RowStyles.Add(new(SizeType.Percent,100)); scan.Controls.Add(scanGrid);
        input=new(){Name="ScanInput",AutoSize=false,Dock=DockStyle.Fill,Margin=new(0,0,10,0),Font=MakeFont(18,FontStyle.Bold),ImeMode=ImeMode.Disable,MaxLength=512,AccessibleName="Housing 或物料扫码"};
        queryButton=Button("查询",true); queryButton.Name="QueryButton"; queryButton.Dock=DockStyle.Fill; queryButton.Margin=new(0,0,8,0);
        cancelButton=Button("取消",false); cancelButton.Dock=DockStyle.Fill; cancelButton.Margin=new(0,0,8,0); cancelButton.Enabled=false;
        status=new(){Dock=DockStyle.Fill,Text="准备就绪",TextAlign=ContentAlignment.MiddleCenter,Margin=Padding.Empty};
        scanGrid.Controls.Add(input,0,0); scanGrid.Controls.Add(queryButton,1,0); scanGrid.Controls.Add(cancelButton,2,0); scanGrid.Controls.Add(status,3,0);
        var cards=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new(18,4,18,8)}; cards.ColumnStyles.Add(new(SizeType.Percent,50)); cards.ColumnStyles.Add(new(SizeType.Percent,50)); cards.RowStyles.Add(new(SizeType.Percent,100)); root.Controls.Add(cards,0,2);
        demolish=new("能否拆除"){Name="VerdictDemolish",Dock=DockStyle.Fill,Margin=new(0,0,8,0)}; clear=new("能否清胶"){Name="VerdictClear",Dock=DockStyle.Fill,Margin=new(8,0,0,0)}; cards.Controls.Add(demolish,0,0); cards.Controls.Add(clear,1,0);
        var detailHost=Surface(); detailHost.Margin=new(18,0,18,8); detailHost.Padding=new(16,8,16,8); root.Controls.Add(detailHost,0,3);
        detail=new(){Dock=DockStyle.Fill,Text="点胶时间 —    本机时间 —    已过 —",TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true}; detailHost.Controls.Add(detail);
        board=new(){Name="ReworkBoard",Dock=DockStyle.Fill,Margin=new(18,0,18,8)}; root.Controls.Add(board,0,4);
        var logHost=new System.Windows.Forms.Panel{Name="LogHost",Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new(18,0,18,16)}; root.Controls.Add(logHost,0,5);
        var logWrap=Surface(); logWrap.Padding=new(14,10,14,10); logHost.Controls.Add(logWrap);
        var logTitle=new Label{Text="最近记录",Dock=DockStyle.Top,Height=26,Font=MakeFont(10,FontStyle.Bold)};
        log=new(){Name="RecentLog",Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.FromArgb(248,250,252),Font=MakeFont(9),MaxLength=65536}; logWrap.Controls.Add(log); logWrap.Controls.Add(logTitle);
        clockTimer.Tick+=(_,_)=>clock.Text=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); clockTimer.Start(); clock.Text=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        InitializeOperations();
    }
    Font MakeFont(float size,FontStyle style=FontStyle.Regular) { var f=new Font("Microsoft YaHei UI",size,style); ownedFonts.Add(f); return f; }
    static AntdPanel Surface()=>new(){Dock=DockStyle.Fill,Back=Color.White,Radius=10,BorderWidth=1,BorderColor=Color.FromArgb(226,232,240)};
    AntdButton Button(string text,bool primary)=>new(){Text=text,Height=40,WaveSize=0,Radius=6,Type=primary?AntdUI.TTypeMini.Primary:AntdUI.TTypeMini.Default,Font=MakeFont(11,FontStyle.Bold),UseMnemonic=false};
    protected override void Dispose(bool disposing)
    {
        if(disposing) { clockTimer.Dispose(); DisposeOperations(); }
        base.Dispose(disposing);
        if(disposing) foreach(var font in ownedFonts) font.Dispose();
    }
}
public sealed class VerdictCard : Control
{
    readonly string title;
    readonly Font titleFont=new("Microsoft YaHei UI",16,FontStyle.Bold), verdictFont=new("Microsoft YaHei UI",48,FontStyle.Bold);
    public string VerdictText {get;private set;}="待查询";
    Color ink=Color.FromArgb(100,116,139);
    public VerdictCard(string title) { this.title=title; DoubleBuffered=true; BackColor=Color.White; }
    public void Apply(string text,bool? allowed=null)
    {
        VerdictText=text; ink=allowed is true?Color.FromArgb(22,163,74):allowed is false?Color.FromArgb(220,38,38):Color.FromArgb(100,116,139);
        BackColor=allowed is true?Color.FromArgb(236,253,243):allowed is false?Color.FromArgb(254,242,242):Color.White; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); using var pen=new Pen(Color.FromArgb(226,232,240)); e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
        TextRenderer.DrawText(e.Graphics,title,titleFont,new Rectangle(24,18,Width-48,40),ink,TextFormatFlags.Left);
        TextRenderer.DrawText(e.Graphics,VerdictText,verdictFont,new Rectangle(8,58,Width-16,Math.Max(10,Height-70)),ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
    protected override void Dispose(bool disposing) { if(disposing) { titleFont.Dispose(); verdictFont.Dispose(); } base.Dispose(disposing); }
}
public sealed class ReworkBoard : System.Windows.Forms.Panel
{
    readonly FlowLayoutPanel flow=new(){Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true,BackColor=Color.White,Margin=Padding.Empty};
    readonly Dictionary<string,StationDonut> cards=new(StringComparer.OrdinalIgnoreCase);
    public ReworkBoard() { DoubleBuffered=true; Controls.Add(flow); }
    public void Apply(List<StationRate> rates)
    {
        var keys=rates.Select(r=>r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase); flow.SuspendLayout();
        foreach(var key in cards.Keys.Where(k=>!keys.Contains(k)).ToList()) { var old=cards[key]; flow.Controls.Remove(old); old.Dispose(); cards.Remove(key); }
        foreach(var rate in rates)
        {
            if(!cards.TryGetValue(rate.Key,out var card)) { card=new(){Name="Station:"+rate.Key}; cards.Add(rate.Key,card); flow.Controls.Add(card); }
            card.Apply(rate);
        }
        flow.ResumeLayout();
    }
}
public sealed class StationDonut : Control
{
    StationRate? rate;
    readonly Font nameFont=new("Microsoft YaHei UI",12,FontStyle.Bold), countFont=new("Microsoft YaHei UI",10);
    public StationDonut() { DoubleBuffered=true; Size=new(240,176); Margin=new(8,4,8,4); BackColor=Color.FromArgb(248,250,252); }
    public void Apply(StationRate value) { if(rate==value) return; rate=value; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); if(rate is null) return;
        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        TextRenderer.DrawText(e.Graphics,rate.Name,nameFont,new Rectangle(4,4,Width-8,26),Color.FromArgb(15,23,42),TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);
        var rect=new Rectangle(Width/2-47,32,94,94);
        using var blue=new SolidBrush(Color.FromArgb(47,84,235)); using var orange=new SolidBrush(Color.FromArgb(245,146,43)); using var empty=new SolidBrush(Color.FromArgb(226,232,240)); using var hole=new SolidBrush(BackColor);
        e.Graphics.FillEllipse(rate.Complete&&rate.Produced>0?blue:empty,rect);
        if(rate.Complete&&rate.Produced>0&&rate.Rework>0) e.Graphics.FillPie(orange,rect,-90,360f*rate.Rework/rate.Produced);
        e.Graphics.FillEllipse(hole,new Rectangle(rect.X+20,rect.Y+20,54,54));
        if(rate.Complete)
        {
            TextRenderer.DrawText(e.Graphics,$"良品 {Math.Max(0,rate.Produced-rate.Rework)}   返工 {rate.Rework}",countFont,new Rectangle(0,122,Width,26),ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics,$"返工率 {(rate.Produced==0?"—":$"{100d*rate.Rework/rate.Produced:0.0}%")}",countFont,new Rectangle(0,148,Width,26),ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
        else TextRenderer.DrawText(e.Graphics,"统计不完整 · 未更新",countFont,new Rectangle(0,130,Width,36),ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
    protected override void Dispose(bool disposing) { if(disposing) { nameFont.Dispose(); countFont.Dispose(); } base.Dispose(disposing); }
}
