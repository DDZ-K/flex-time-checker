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
    private bool _relayout;
    private int _seq;

    public ReworkBoard()
    {
        DoubleBuffered = true;
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
        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0)
        };
        wrap.Controls.Add(_flow);
        Controls.Add(wrap);
        Resize += (_, _) =>
        {
            if (_relayout || _last.Count == 0)
                return;
            _relayout = true;
            try { ShowRates(_last); }
            finally { _relayout = false; }
        };
    }

    public void ShowRates(IReadOnlyList<StationRate> rates)
    {
        _last = rates;
        _flow.SuspendLayout();
        _flow.Controls.Clear();
        if (rates.Count == 0)
        {
            _flow.Controls.Add(new Label
            {
                Text = "先在配置里加点胶站",
                AutoSize = true,
                ForeColor = Muted,
                BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei UI", 11f),
                Margin = new Padding(8, 16, 8, 8)
            });
        }
        else
        {
            foreach (var rate in rates)
                _flow.Controls.Add(new StationDonutCard(rate));
        }
        _flow.ResumeLayout();
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

internal sealed class StationDonutCard : Panel
{
    private static readonly Color Ink = Color.FromArgb(15, 23, 42);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);
    private static readonly Color Line = Color.FromArgb(226, 232, 240);
    private static readonly Color GoodBlue = Color.FromArgb(47, 84, 235);
    private static readonly Color ReworkOrange = Color.FromArgb(245, 146, 43);
    private static readonly Color Empty = Color.FromArgb(226, 232, 240);
    private static readonly Color Card = Color.FromArgb(248, 250, 252);

    public StationDonutCard(StationRate rate)
    {
        DoubleBuffered = true;
        Width = 320;
        Height = 352;
        Margin = new Padding(10, 4, 10, 4);
        BackColor = Color.Transparent;
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

        var good = Math.Max(0, rate.Produced - rate.Rework);
        var rework = Math.Max(0, rate.Rework);
        var rateText = rate.Produced <= 0 ? "—" : (rate.Rate <= 0 ? "0%" : $"{rate.Rate * 100:0.0}%");

        var name = new Label
        {
            Text = rate.Name,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
            ForeColor = Ink,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var donut = new DonutView(good, rework)
        {
            Dock = DockStyle.Top,
            Height = 196,
            BackColor = Color.Transparent
        };
        var counts = new Label
        {
            Text = $"良品 {good}    返工 {rework}",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Microsoft YaHei UI", 12f),
            ForeColor = Muted,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var pct = new Label
        {
            Text = $"返工率 {rateText}",
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold),
            ForeColor = Ink,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(pct);
        Controls.Add(counts);
        Controls.Add(donut);
        Controls.Add(name);
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
    private readonly int _good;
    private readonly int _rework;

    public DonutView(int good, int rework)
    {
        _good = Math.Max(0, good);
        _rework = Math.Max(0, rework);
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
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
