using AntdButton = AntdUI.Button;
using AntdPanel = AntdUI.Panel;

namespace FlexTimeChecker;

internal sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(246, 248, 251);
    private static readonly Color Surface = Color.White;
    private static readonly Color Line = Color.FromArgb(226, 232, 240);
    private static readonly Color Ink = Color.FromArgb(15, 23, 42);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);
    private static readonly Color Blue = Color.FromArgb(22, 119, 255);
    private static readonly Color Ok = Color.FromArgb(22, 163, 74);
    private static readonly Color Ng = Color.FromArgb(220, 38, 38);
    private static readonly Color Wait = Color.FromArgb(217, 119, 6);

    private AppConfig _cfg;
    private readonly TextBox _housing;
    private readonly AntdButton _queryBtn;
    private readonly Label _status;
    private readonly Label _clock;
    private readonly VerdictCard _demolishCard;
    private readonly VerdictCard _clearCard;
    private readonly Label _timeDetail;
    private readonly TextBox _log;
    private readonly ReworkBoard _rework;
    private bool _busy;

    public MainForm()
    {
        _cfg = AppConfig.Load();

        Text = "Flex 点胶时间检查";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 700);
        Size = new Size(1280, 800);
        BackColor = Bg;
        ForeColor = Ink;
        Font = new Font("Microsoft YaHei UI", 10f);
        DoubleBuffered = true;
        Branding.Apply(this);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Surface,
            Padding = new Padding(18, 14, 22, 12)
        };
        var configButton = MakeButton("配置", AntdUI.TTypeMini.Default, 88);
        configButton.Location = new Point(18, 16);
        configButton.Click += (_, _) => OpenConfigDialog();

        var logo = Branding.MakeHeaderLogo(118, 16);
        if (logo is not null)
            header.Controls.Add(logo);
        _clock = new Label
        {
            Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Dock = DockStyle.Right,
            Width = 280,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            ForeColor = Muted,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 8, 0)
        };
        header.Controls.Add(configButton);
        header.Controls.Add(_clock);

        var accent = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Blue };

        var scanWrap = new AntdPanel
        {
            Dock = DockStyle.Fill,
            Back = Surface,
            Radius = 10,
            BorderWidth = 1,
            BorderColor = Line,
            InnerPadding = new Padding(12, 12, 12, 12)
        };
        var scanHost = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Bg, Padding = new Padding(18, 14, 18, 0) };
        _housing = new TextBox
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Consolas", 18f, FontStyle.Bold),
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Ink,
            BorderStyle = BorderStyle.FixedSingle,
            ImeMode = ImeMode.Disable,
            Margin = new Padding(0, 0, 10, 0)
        };
        _housing.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Query();
            }
        };
        _queryBtn = MakeButton("查询", AntdUI.TTypeMini.Primary, 120);
        _queryBtn.Dock = DockStyle.Fill;
        _queryBtn.Margin = new Padding(0, 0, 10, 0);
        _queryBtn.Click += (_, _) => Query();
        _status = new Label
        {
            Text = "准备就绪",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            ForeColor = Muted,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        var scanInner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        scanInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scanInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        scanInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        scanInner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scanInner.Controls.Add(_housing, 0, 0);
        scanInner.Controls.Add(_queryBtn, 1, 0);
        scanInner.Controls.Add(_status, 2, 0);
        scanWrap.Controls.Add(scanInner);
        scanHost.Controls.Add(scanWrap);

        var logWrap = new AntdPanel
        {
            Dock = DockStyle.Fill,
            Back = Surface,
            Radius = 10,
            BorderWidth = 1,
            BorderColor = Line,
            InnerPadding = new Padding(14, 10, 14, 10)
        };
        var logHost = new Panel { Dock = DockStyle.Bottom, Height = 196, BackColor = Bg, Padding = new Padding(18, 0, 18, 16) };
        var logTitle = new Label
        {
            Text = "最近记录",
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Ink,
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(51, 65, 85),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9f)
        };
        logWrap.Controls.Add(_log);
        logWrap.Controls.Add(logTitle);
        logHost.Controls.Add(logWrap);

        var detailHost = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Bg, Padding = new Padding(18, 0, 18, 10) };
        var detail = new AntdPanel
        {
            Dock = DockStyle.Fill,
            Back = Surface,
            Radius = 10,
            BorderWidth = 1,
            BorderColor = Line,
            InnerPadding = new Padding(18, 8, 18, 8)
        };
        _timeDetail = new Label
        {
            Text = "点胶时间 —    本机时间 —    已过 —",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 12f),
            ForeColor = Ink,
            BackColor = Color.Transparent
        };
        detail.Controls.Add(_timeDetail);
        detailHost.Controls.Add(detail);

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18, 8, 18, 8),
            BackColor = Bg
        };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _demolishCard = new VerdictCard("能否拆除") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
        _clearCard = new VerdictCard("能否清胶") { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0) };
        cards.Controls.Add(_demolishCard, 0, 0);
        cards.Controls.Add(_clearCard, 1, 0);

        _rework = new ReworkBoard { Dock = DockStyle.Bottom };

        Controls.Add(cards);
        Controls.Add(detailHost);
        Controls.Add(_rework);
        Controls.Add(logHost);
        Controls.Add(scanHost);
        Controls.Add(accent);
        Controls.Add(header);

        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => _clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        timer.Start();

        Shown += (_, _) =>
        {
            _housing.Focus();
            Log($"配置已加载  点胶站 {_cfg.GetDispenseProfiles().Count}  组装站 {_cfg.GetBindProfiles().Count}");
            _rework.RefreshAsync(_cfg, this);
        };

        var reworkTimer = new System.Windows.Forms.Timer { Interval = 60000 };
        reworkTimer.Tick += (_, _) => _rework.RefreshAsync(_cfg, this);
        reworkTimer.Start();
    }

    private void Query()
    {
        if (_busy) return;
        var code = _housing.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            _status.Text = "请先扫码";
            _status.ForeColor = Wait;
            return;
        }

        _busy = true;
        _queryBtn.Enabled = false;
        _status.Text = "搜索中";
        _status.ForeColor = Blue;
        _demolishCard.SetPending();
        _clearCard.SetPending();
        Log($"开始搜索 {code}");

        var cfg = _cfg;
        Task.Run(() =>
        {
            var result = RequestXmlSearch.Find(code, cfg);
            BeginInvoke(new Action(() => ApplyResult(code, result, cfg)));
        });
    }

    private void ApplyResult(string code, SearchResult result, AppConfig cfg)
    {
        _busy = false;
        _queryBtn.Enabled = true;
        _housing.SelectAll();
        _housing.Focus();

        if (!result.Found)
        {
            _status.Text = "未找到";
            _status.ForeColor = Ng;
            _demolishCard.SetUnknown();
            _clearCard.SetUnknown();
            _timeDetail.Text = result.Error ?? "未找到";
            Log($"未找到 {code}  扫了 {result.FilesScanned} 个文件  {result.ElapsedMs}ms");
            _rework.RefreshAsync(cfg, this);
            return;
        }

        if (result.DispenseTime is not DateTime dispense)
        {
            _status.Text = "无点胶时间";
            _status.ForeColor = Wait;
            _demolishCard.SetUnknown();
            _clearCard.SetUnknown();
            _timeDetail.Text = "命中报文，但没解析到时间";
            Log($"命中文件但无时间：{result.FilePath}");
            return;
        }

        var profile = result.Profile;
        if (profile is null)
        {
            _status.Text = "配置错误";
            _status.ForeColor = Ng;
            _demolishCard.SetUnknown();
            _clearCard.SetUnknown();
            _timeDetail.Text = "报文找到了，但没有关联目录配置";
            return;
        }

        var housing = string.IsNullOrWhiteSpace(result.HousingCode) ? code : result.HousingCode;
        var scannedKind = string.IsNullOrWhiteSpace(result.MaterialCode)
            ? $"Housing {housing}"
            : $"物料 {result.MaterialCode} → Housing {housing}";

        var now = DateTime.Now;
        var elapsed = now - dispense;
        var remainClear = TimeSpan.FromMinutes(profile.ClearGlueMinutes) - elapsed;
        var demolishOk = WaitRules.CanDemolish(elapsed, profile.DemolishMinutes);
        var clearOk = WaitRules.CanClearGlue(elapsed, profile.ClearGlueMinutes);

        _demolishCard.SetVerdict(demolishOk);
        _clearCard.SetVerdict(clearOk);

        _status.Text = "已命中";
        _status.ForeColor = Ok;
        var demolishWait = demolishOk ? "拆除可以" : "已过拆除窗口";
        var clearWait = clearOk ? "清胶可以" : $"清胶还需 {FormatSpan(remainClear)}";
        _timeDetail.Text =
            $"{scannedKind}    点胶 {dispense:yyyy-MM-dd HH:mm:ss}    本机 {now:yyyy-MM-dd HH:mm:ss}    已过 {FormatSpan(elapsed)}    {demolishWait}    {clearWait}";
        Log($"命中 {scannedKind}  目录={profile.Name}  点胶={dispense:yyyy-MM-dd HH:mm:ss}  已过={FormatSpan(elapsed)}  拆除={(demolishOk ? "可以" : "不可以")}  清胶={(clearOk ? "可以" : "不可以")}  扫描={result.FilesScanned}个 Request XML  {result.ElapsedMs}ms");
        var added = ReworkTracker.TryRecord(result, cfg);
        if (added)
            Log($"返工已计  Housing {housing}");
        _rework.RefreshAsync(cfg, this);
    }

    private void OpenConfigDialog()
    {
        using var dialog = new FolderProfilesDialog(_cfg.GetProfiles());
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _cfg.Folders = dialog.Profiles.Select(x => x.Clone()).ToList();
        _cfg.Save();
        _status.Text = "配置已保存";
        _status.ForeColor = Ok;
        Log($"已保存 点胶站 {_cfg.GetDispenseProfiles().Count}  组装站 {_cfg.GetBindProfiles().Count}");
        _rework.RefreshAsync(_cfg, this);
    }

    private void Log(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    internal static string FormatSpan(TimeSpan span)
    {
        if (span.TotalSeconds < 0)
            return "0分";
        if (span.TotalHours >= 24)
            return $"{(int)span.TotalDays}天 {span.Hours}小时 {span.Minutes}分";
        if (span.TotalMinutes >= 60)
            return $"{(int)span.TotalHours}小时 {span.Minutes}分";
        return $"{(int)span.TotalMinutes}分 {span.Seconds}秒";
    }

    private static AntdButton MakeButton(string text, AntdUI.TTypeMini type, int width)
    {
        var button = new AntdButton
        {
            Text = text,
            Type = type,
            Width = width,
            Height = 40,
            Radius = 6,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            UseMnemonic = false
        };
        if (type == AntdUI.TTypeMini.Default)
        {
            button.DefaultBack = Surface;
            button.DefaultBorderColor = Line;
            button.BorderWidth = 1;
            button.ForeColor = Ink;
        }
        return button;
    }
}

internal sealed class VerdictCard : Panel
{
    private readonly Label _title;
    private readonly Label _verdict;
    private Color _fill = Color.White;
    private Color _border = Color.FromArgb(226, 232, 240);

    public VerdictCard(string title)
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(246, 248, 251);
        Padding = new Padding(8);

        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(246, 248, 251),
            Padding = new Padding(28, 18, 28, 18)
        };
        inner.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(_fill);
            using var pen = new Pen(_border);
            var rect = new Rectangle(0, 0, inner.Width - 1, inner.Height - 1);
            using var path = Round(rect, 12);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        };

        _title = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _verdict = new Label
        {
            Text = "待查询",
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 56f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        inner.Controls.Add(_verdict);
        inner.Controls.Add(_title);
        Controls.Add(inner);
        Resize += (_, _) =>
        {
            ScaleVerdict();
            inner.Invalidate();
        };
    }

    public void SetPending()
    {
        Apply("搜索中", Color.FromArgb(22, 119, 255), Color.FromArgb(239, 246, 255), Color.FromArgb(191, 219, 254));
    }

    public void SetUnknown()
    {
        Apply("未知", Color.FromArgb(217, 119, 6), Color.FromArgb(255, 251, 235), Color.FromArgb(253, 230, 138));
    }

    public void SetVerdict(bool ok)
    {
        if (ok)
            Apply("可以", Color.FromArgb(22, 163, 74), Color.FromArgb(236, 253, 243), Color.FromArgb(134, 239, 172));
        else
            Apply("不可以", Color.FromArgb(220, 38, 38), Color.FromArgb(254, 242, 242), Color.FromArgb(254, 202, 202));
    }

    private void Apply(string text, Color ink, Color back, Color border)
    {
        _verdict.Text = text;
        _verdict.ForeColor = ink;
        _title.ForeColor = ink;
        _fill = back;
        _border = border;
        if (Controls.Count > 0)
            Controls[0].Invalidate();
        ScaleVerdict();
        Invalidate();
    }

    private void ScaleVerdict()
    {
        var h = Math.Max(28, Height - 100);
        var w = Math.Max(28, Width - 80);
        var size = Math.Clamp(Math.Min(h / 3.2f, w / 5.2f), 18f, 120f);
        if (_verdict.Text == "不可以")
            size *= 0.82f;
        _verdict.Font = new Font("Microsoft YaHei UI", size, FontStyle.Bold);
    }

    private static System.Drawing.Drawing2D.GraphicsPath Round(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
