using System.Drawing.Drawing2D;

namespace FlexRequestExtractor;

internal sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(11, 16, 28);
    private static readonly Color Surface = Color.FromArgb(18, 26, 43);
    private static readonly Color Accent = Color.FromArgb(61, 220, 255);
    private static readonly Color TextMain = Color.FromArgb(232, 238, 247);
    private static readonly Color Muted = Color.FromArgb(139, 151, 168);
    private static readonly Color Ok = Color.FromArgb(50, 213, 131);
    private static readonly Color Ng = Color.FromArgb(249, 112, 102);
    private static readonly Color Wait = Color.FromArgb(245, 196, 81);

    private AppConfig _cfg;
    private ExtractEngine? _engine;
    private readonly Button _startBtn;
    private readonly Label _status;
    private readonly Label _clock;
    private readonly Label _stationHint;
    private readonly StatCard _copiedCard;
    private readonly StatCard _failedCard;
    private readonly Label _detail;
    private readonly TextBox _log;

    public MainForm()
    {
        _cfg = AppConfig.Load();

        Text = "Flex 报文提取";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 640);
        Size = new Size(1080, 700);
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Microsoft YaHei UI", 10f);
        DoubleBuffered = true;
        Branding.Apply(this);

        var header = MakePanel(0, 0, 1080, 64);
        header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var configButton = MakeGhostButton("配置", 24, 12, 94, 40);
        configButton.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        configButton.Click += (_, _) => OpenConfig();
        var titleX = 138;
        var logo = Branding.MakeHeaderLogo(132, 12);
        if (logo is not null)
        {
            header.Controls.Add(logo);
            titleX = 356;
        }
        var title = MakeLabel("FLEX 报文提取", titleX, 16, 360, 36, Accent, 16, FontStyle.Bold);
        _clock = MakeLabel(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), 760, 18, 290, 28, Accent, 12, FontStyle.Bold);
        _clock.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _clock.TextAlign = ContentAlignment.MiddleRight;
        header.Controls.Add(configButton);
        header.Controls.Add(title);
        header.Controls.Add(_clock);

        var bar = MakePanel(16, 80, 1048, 72);
        bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _startBtn = new Button
        {
            Text = "开始提取",
            Location = new Point(16, 14),
            Size = new Size(160, 44),
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.FromArgb(8, 16, 24),
            Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _startBtn.FlatAppearance.BorderSize = 0;
        _startBtn.Click += (_, _) => Toggle();
        _status = MakeLabel("已停止", 196, 22, 180, 28, Muted, 14, FontStyle.Bold);
        _stationHint = MakeLabel(StationHintText(), 390, 24, 630, 26, Muted, 11);
        _stationHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        bar.Controls.Add(_startBtn);
        bar.Controls.Add(_status);
        bar.Controls.Add(_stationHint);

        _copiedCard = new StatCard("已复制") { Location = new Point(16, 168), Size = new Size(512, 160) };
        _failedCard = new StatCard("失败") { Location = new Point(552, 168), Size = new Size(512, 160) };

        var detail = MakePanel(16, 344, 1048, 56);
        detail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _detail = MakeLabel("最近 —", 16, 14, 1016, 28, TextMain, 11);
        _detail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        detail.Controls.Add(_detail);

        _log = new TextBox
        {
            Location = new Point(16, 416),
            Size = new Size(1048, 228),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(10, 14, 24),
            ForeColor = Muted,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9f),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        Controls.Add(header);
        Controls.Add(bar);
        Controls.Add(_copiedCard);
        Controls.Add(_failedCard);
        Controls.Add(detail);
        Controls.Add(_log);

        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => _clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        timer.Start();

        Resize += (_, _) => LayoutCards();
        FormClosing += (_, e) =>
        {
            _engine?.Stop();
        };
        Shown += (_, _) =>
        {
            LayoutCards();
            Log($"已加载配置  点胶 {_cfg.DispenseItems.Count}  组装 {_cfg.BindItems.Count}");
        };
    }

    private void LayoutCards()
    {
        const int gap = 16;
        var w = Math.Max(300, (ClientSize.Width - gap * 3) / 2);
        _copiedCard.Location = new Point(gap, 168);
        _copiedCard.Size = new Size(w, 160);
        _failedCard.Location = new Point(gap * 2 + w, 168);
        _failedCard.Size = new Size(Math.Max(300, ClientSize.Width - gap * 3 - w), 160);
    }

    private string StationHintText()
    {
        var d = _cfg.DispenseItems.Count;
        var b = _cfg.BindItems.Count;
        return d == 0 && b == 0
            ? "未配置站位，请先点左上角配置"
            : $"点胶 {d}  组装 {b}";
    }

    private void Toggle()
    {
        if (_engine is { IsRunning: true })
        {
            _engine.Stop();
            SetStoppedUi();
            return;
        }

        _cfg = AppConfig.Load();
        if (string.IsNullOrWhiteSpace(_cfg.WatchRoot))
        {
            MessageBox.Show(this, "请先在配置里填写报文目录。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_cfg.DispenseItems.Count == 0 && _cfg.BindItems.Count == 0)
        {
            MessageBox.Show(this, "请先在配置里添加点胶站或组装站。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _engine?.Stop();
        _engine = new ExtractEngine(_cfg);
        _engine.Logged += msg =>
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() => Log(msg)));
        };
        _engine.StatsChanged += () =>
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(RefreshStats));
        };
        if (!_engine.Start())
        {
            MessageBox.Show(this, "已有提取进程在运行。若已安装 Windows 服务，请不要再点开始提取。", "提取", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetStoppedUi();
            return;
        }
        _startBtn.Text = "停止提取";
        _startBtn.BackColor = Ng;
        _startBtn.ForeColor = Color.White;
        _status.Text = "监控中";
        _status.ForeColor = Ok;
        _stationHint.Text = StationHintText();
        RefreshStats();
    }

    private void SetStoppedUi()
    {
        _startBtn.Text = "开始提取";
        _startBtn.BackColor = Accent;
        _startBtn.ForeColor = Color.FromArgb(8, 16, 24);
        _status.Text = "已停止";
        _status.ForeColor = Muted;
        _stationHint.Text = StationHintText();
    }

    private void RefreshStats()
    {
        if (_engine is null) return;
        _copiedCard.SetValue(_engine.Copied.ToString());
        _failedCard.SetValue(_engine.Failed.ToString(), _engine.Failed > 0 ? Ng : TextMain);
        _detail.Text = string.IsNullOrEmpty(_engine.LastFile)
            ? $"最近 —    过期删除 {_engine.Deleted}"
            : $"最近 {_engine.LastFile}    站位 {_engine.LastStation}    跳过 {_engine.Skipped}    过期删除 {_engine.Deleted}";
    }

    private void OpenConfig()
    {
        if (_engine is { IsRunning: true })
        {
            MessageBox.Show(this, "请先停止提取再改配置。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new ConfigDialog(_cfg);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _cfg = dialog.Config;
        _cfg.Save();
        _stationHint.Text = StationHintText();
        Log($"已保存配置  点胶 {_cfg.DispenseItems.Count}  组装 {_cfg.BindItems.Count}  保留 {_cfg.KeepDays} 天");
    }

    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        if (_log.TextLength > 0)
            _log.AppendText(Environment.NewLine + line);
        else
            _log.Text = line;
        var max = Math.Max(50, _cfg.MaxLogLines);
        var lines = _log.Lines;
        if (lines.Length > max)
            _log.Lines = lines.Skip(lines.Length - max).ToArray();
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private static Panel MakePanel(int x, int y, int w, int h)
    {
        return new Panel
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = Surface
        };
    }

    private static Label MakeLabel(string text, int x, int y, int w, int h, Color color, float size, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            ForeColor = color,
            Font = new Font("Microsoft YaHei UI", size, style),
            AutoSize = false
        };
    }

    private static Button MakeGhostButton(string text, int x, int y, int w, int h)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 55, 78),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}

internal sealed class StatCard : Panel
{
    private readonly Label _title;
    private readonly Label _value;

    public StatCard(string title)
    {
        BackColor = Color.FromArgb(18, 26, 43);
        _title = new Label
        {
            Text = title,
            Location = new Point(24, 18),
            Size = new Size(200, 28),
            ForeColor = Color.FromArgb(139, 151, 168),
            Font = new Font("Microsoft YaHei UI", 12f)
        };
        _value = new Label
        {
            Text = "0",
            Location = new Point(24, 56),
            Size = new Size(460, 80),
            ForeColor = Color.FromArgb(232, 238, 247),
            Font = new Font("Microsoft YaHei UI", 36f, FontStyle.Bold)
        };
        Controls.Add(_title);
        Controls.Add(_value);
    }

    public void SetValue(string text, Color? color = null)
    {
        _value.Text = text;
        if (color is Color c)
            _value.ForeColor = c;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(38, 54, 82));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        _value.Size = new Size(Math.Max(80, Width - 48), 80);
    }
}
