using AntdPanel = AntdUI.Panel;

namespace FlexTimeChecker;

internal sealed class ReworkBoard : Panel
{
    private static readonly Color Bg = Color.FromArgb(246, 248, 251);
    private static readonly Color Surface = Color.White;
    private static readonly Color Line = Color.FromArgb(226, 232, 240);
    private static readonly Color Ink = Color.FromArgb(15, 23, 42);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);

    private readonly FlowLayoutPanel _flow;
    private IReadOnlyList<StationRate> _last = [];
    private int _seq;

    public ReworkBoard()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Bg;
        Padding = new Padding(18, 0, 18, 10);
        Height = 380;

        var wrap = new AntdPanel
        {
            Dock = DockStyle.Fill,
            Back = Surface,
            Radius = 10,
            BorderWidth = 1,
            BorderColor = Line,
            InnerPadding = new Padding(10, 8, 10, 8)
        };
        _flow = new SilentFlow
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0)
        };
        wrap.Controls.Add(_flow);
        Controls.Add(wrap);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000;
            return cp;
        }
    }

    public void ShowRates(IReadOnlyList<StationRate> rates)
    {
        if (Same(_last, rates) && HasDonuts())
            return;
        _last = rates;

        if (rates.Count == 0)
        {
            ShowEmpty();
            return;
        }

        var cards = new List<StationDonutCard>();
        foreach (Control c in _flow.Controls)
        {
            if (c is StationDonutCard card)
                cards.Add(card);
        }

        if (cards.Count == _flow.Controls.Count && cards.Count > 0)
        {
            var keep = Math.Min(cards.Count, rates.Count);
            for (var i = 0; i < keep; i++)
                cards[i].Apply(rates[i]);
            for (var i = cards.Count; i < rates.Count; i++)
                _flow.Controls.Add(new StationDonutCard(rates[i]));
            for (var i = _flow.Controls.Count - 1; i >= rates.Count; i--)
                _flow.Controls.RemoveAt(i);
            return;
        }

        Rebuild(rates);
    }

    private bool HasDonuts()
        => _flow.Controls.Count > 0 && _flow.Controls[0] is StationDonutCard;

    private void ShowEmpty()
    {
        if (_flow.Controls.Count == 1 && _flow.Controls[0] is Label)
            return;
        _flow.SuspendLayout();
        _flow.Controls.Clear();
        _flow.Controls.Add(new Label
        {
            Text = "先在配置里加点胶站",
            AutoSize = true,
            ForeColor = Muted,
            BackColor = Surface,
            Font = new Font("Microsoft YaHei UI", 11f),
            Margin = new Padding(8, 16, 8, 8)
        });
        _flow.ResumeLayout();
    }

    private void Rebuild(IReadOnlyList<StationRate> rates)
    {
        _flow.SuspendLayout();
        _flow.Controls.Clear();
        foreach (var rate in rates)
            _flow.Controls.Add(new StationDonutCard(rate));
        _flow.ResumeLayout();
    }

    private static bool Same(IReadOnlyList<StationRate> a, IReadOnlyList<StationRate> b)
    {
        if (a.Count != b.Count)
            return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i].Name, b[i].Name, StringComparison.Ordinal)
                || a[i].Produced != b[i].Produced
                || a[i].Rework != b[i].Rework)
                return false;
        }
        return true;
    }

    public void RefreshAsync(AppConfig cfg, Control marshal)
    {
        var seq = Interlocked.Increment(ref _seq);
        Task.Run(() =>
        {
            var snap = ReworkTracker.Snapshot(cfg);
            if (seq != Volatile.Read(ref _seq) || marshal.IsDisposed)
                return;
            try
            {
                marshal.BeginInvoke(new Action(() =>
                {
                    if (seq != _seq || IsDisposed)
                        return;
                    ShowRates(snap);
                }));
            }
            catch (ObjectDisposedException)
            {
            }
        });
    }
}

internal sealed class SilentFlow : FlowLayoutPanel
{
    public SilentFlow()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000;
            return cp;
        }
    }
}

internal sealed class StationDonutCard : Panel
{
    private static readonly Color Ink = Color.FromArgb(15, 23, 42);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);
    private static readonly Color Line = Color.FromArgb(226, 232, 240);
    private static readonly Color GoodBlue = Color.FromArgb(47, 84, 235);
    private static readonly Color ReworkOrange = Color.FromArgb(245, 146, 43);
    private static readonly Color Empty = Color.FromArgb(226, 232, 240);
    private static readonly Color Card = Color.FromArgb(248, 250, 252);

    private readonly Label _name;
    private readonly DonutView _donut;
    private readonly Label _counts;
    private readonly Label _pct;

    public StationDonutCard(StationRate rate)
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Width = 320;
        Height = 352;
        Margin = new Padding(10, 4, 10, 4);
        BackColor = Card;
        Padding = new Padding(12, 10, 12, 10);

        Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Card);
            using var pen = new Pen(Line);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Round(rect, 10);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        };

        _name = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
            ForeColor = Ink,
            BackColor = Card,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _donut = new DonutView
        {
            Dock = DockStyle.Top,
            Height = 196,
            BackColor = Card
        };
        _counts = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Microsoft YaHei UI", 12f),
            ForeColor = Muted,
            BackColor = Card,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _pct = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold),
            ForeColor = Ink,
            BackColor = Card,
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(_pct);
        Controls.Add(_counts);
        Controls.Add(_donut);
        Controls.Add(_name);
        Apply(rate);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000;
            return cp;
        }
    }

    public void Apply(StationRate rate)
    {
        var good = Math.Max(0, rate.Produced - rate.Rework);
        var rework = Math.Max(0, rate.Rework);
        var rateText = rate.Produced <= 0 ? "—" : (rate.Rate <= 0 ? "0%" : $"{rate.Rate * 100:0.0}%");
        SetText(_name, rate.Name);
        SetText(_counts, $"良品 {good}    返工 {rework}");
        SetText(_pct, $"返工率 {rateText}");
        _donut.SetValues(good, rework);
    }

    private static void SetText(Label label, string text)
    {
        if (label.Text != text)
            label.Text = text;
    }

    internal static Color SliceGood => GoodBlue;
    internal static Color SliceRework => ReworkOrange;
    internal static Color SliceEmpty => Empty;

    internal static System.Drawing.Drawing2D.GraphicsPath Round(Rectangle rect, int radius)
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

internal sealed class DonutView : Panel
{
    private int _good;
    private int _rework;

    public DonutView()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(248, 250, 252);
    }

    public void SetValues(int good, int rework)
    {
        good = Math.Max(0, good);
        rework = Math.Max(0, rework);
        if (_good == good && _rework == rework)
            return;
        _good = good;
        _rework = rework;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        var size = Math.Min(Width, Height) - 8;
        if (size < 20)
            return;
        var x = (Width - size) / 2;
        var y = (Height - size) / 2;
        var outer = new Rectangle(x, y, size, size);
        var hole = (int)Math.Round(size * 0.56);
        var inner = new Rectangle(x + (size - hole) / 2, y + (size - hole) / 2, hole, hole);
        var total = _good + _rework;

        if (total <= 0)
        {
            using var empty = new SolidBrush(StationDonutCard.SliceEmpty);
            g.FillEllipse(empty, outer);
        }
        else
        {
            var reworkSweep = 360f * _rework / total;
            if (_rework > 0 && reworkSweep < 8f)
                reworkSweep = 8f;
            if (_good == 0)
                reworkSweep = 360f;
            using var goodBrush = new SolidBrush(StationDonutCard.SliceGood);
            using var reworkBrush = new SolidBrush(StationDonutCard.SliceRework);
            g.FillEllipse(goodBrush, outer);
            if (_rework > 0)
                g.FillPie(reworkBrush, outer, -90, reworkSweep);
        }

        using var holeBrush = new SolidBrush(Color.FromArgb(248, 250, 252));
        g.FillEllipse(holeBrush, inner);
    }
}
