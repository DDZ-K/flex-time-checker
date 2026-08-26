namespace FlexRequestExtractor;

internal sealed class ConfigDialog : Form
{
    private readonly TextBox _watch;
    private readonly ListBox _dispenseList;
    private readonly ListBox _bindList;
    private readonly TextBox _pathBox;
    private readonly NumericUpDown _poll;
    private readonly NumericUpDown _keepDays;
    private readonly CheckBox _onlyNew;
    private readonly List<StationTarget> _dispense;
    private readonly List<StationTarget> _bind;

    public AppConfig Config { get; }

    public ConfigDialog(AppConfig source)
    {
        Config = source.Clone();
        _dispense = Config.DispenseItems.Select(x => x.Clone()).ToList();
        _bind = Config.BindItems.Select(x => x.Clone()).ToList();

        Text = "配置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(980, 550);
        Branding.Apply(this);
        BackColor = Color.FromArgb(18, 26, 43);
        ForeColor = Color.FromArgb(232, 238, 247);
        Font = new Font("Microsoft YaHei UI", 10f);

        Controls.Add(MakeHeading("报文目录", 24, 20));
        _watch = MakeTextBox(140, 18, 700, Config.WatchRoot);
        var browseWatch = MakeButton("选择", 856, 16, 96, Color.FromArgb(42, 55, 78));
        browseWatch.Click += (_, _) => Browse(_watch);
        Controls.Add(_watch);
        Controls.Add(browseWatch);

        Controls.Add(MakeHeading("点胶站", 24, 70));
        Controls.Add(MakeHeading("组装站", 504, 70));
        _dispenseList = MakeList(24, 102, 456, 210);
        _bindList = MakeList(504, 102, 448, 210);
        _dispenseList.SelectedIndexChanged += (_, _) =>
        {
            if (_dispenseList.SelectedIndex >= 0)
                _bindList.ClearSelected();
            ShowSelectedPath();
        };
        _bindList.SelectedIndexChanged += (_, _) =>
        {
            if (_bindList.SelectedIndex >= 0)
                _dispenseList.ClearSelected();
            ShowSelectedPath();
        };
        _dispenseList.DoubleClick += (_, _) => EditSelected();
        _bindList.DoubleClick += (_, _) => EditSelected();
        Controls.Add(_dispenseList);
        Controls.Add(_bindList);

        Controls.Add(MakeHeading("保存路径", 24, 328));
        _pathBox = new TextBox
        {
            Location = new Point(140, 324),
            Size = new Size(812, 30),
            ReadOnly = true,
            BackColor = Color.FromArgb(10, 14, 24),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10f)
        };
        Controls.Add(_pathBox);

        var addDispense = MakeButton("新增点胶", 24, 372, 120, Color.FromArgb(61, 220, 255));
        addDispense.ForeColor = Color.FromArgb(8, 16, 24);
        addDispense.Click += (_, _) => AddItem(false);
        var addBind = MakeButton("新增组装", 156, 372, 120, Color.FromArgb(61, 220, 255));
        addBind.ForeColor = Color.FromArgb(8, 16, 24);
        addBind.Click += (_, _) => AddItem(true);
        var edit = MakeButton("编辑", 288, 372, 90, Color.FromArgb(42, 55, 78));
        edit.Click += (_, _) => EditSelected();
        var remove = MakeButton("删除", 390, 372, 90, Color.FromArgb(88, 42, 55));
        remove.Click += (_, _) => RemoveSelected();
        Controls.Add(addDispense);
        Controls.Add(addBind);
        Controls.Add(edit);
        Controls.Add(remove);

        Controls.Add(MakeHeading("轮询秒", 24, 432));
        _poll = MakeNumber(140, 428, Config.PollIntervalSec, 1, 60);
        Controls.Add(_poll);
        Controls.Add(MakeHeading("保留天", 300, 432));
        _keepDays = MakeNumber(400, 428, Config.KeepDays, 1, 14);
        Controls.Add(_keepDays);

        _onlyNew = new CheckBox
        {
            Text = "只拷启动后的新报文",
            Location = new Point(560, 432),
            AutoSize = true,
            Checked = Config.OnlyNewAfterStart,
            ForeColor = Color.FromArgb(232, 238, 247),
            Font = new Font("Microsoft YaHei UI", 11f)
        };
        Controls.Add(_onlyNew);

        var cancel = MakeButton("取消", 756, 492, 96, Color.FromArgb(42, 55, 78));
        cancel.DialogResult = DialogResult.Cancel;
        var save = MakeButton("保存", 864, 492, 88, Color.FromArgb(61, 220, 255));
        save.ForeColor = Color.FromArgb(8, 16, 24);
        save.Click += (_, _) =>
        {
            Config.WatchRoot = _watch.Text.Trim();
            Config.DispenseItems = _dispense.Select(x => x.Clone()).ToList();
            Config.BindItems = _bind.Select(x => x.Clone()).ToList();
            Config.PollIntervalSec = (int)_poll.Value;
            Config.KeepDays = (int)_keepDays.Value;
            Config.OnlyNewAfterStart = _onlyNew.Checked;
            if (string.IsNullOrWhiteSpace(Config.WatchRoot))
            {
                MessageBox.Show(this, "请填写报文目录。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (Config.DispenseItems.Count == 0 && Config.BindItems.Count == 0)
            {
                MessageBox.Show(this, "请至少添加一个点胶站或组装站。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (Config.AllItems.Any(x => string.IsNullOrWhiteSpace(x.SaveFolder)))
            {
                MessageBox.Show(this, "每个站位都要填写保存路径。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            Config.Normalize();
        };
        save.DialogResult = DialogResult.OK;
        Controls.Add(cancel);
        Controls.Add(save);
        AcceptButton = save;
        CancelButton = cancel;
        RefreshLists();
    }

    private void RefreshLists()
    {
        Fill(_dispenseList, _dispense);
        Fill(_bindList, _bind);
        ShowSelectedPath();
    }

    private static void Fill(ListBox list, IReadOnlyList<StationTarget> items)
    {
        var selected = list.SelectedIndex;
        list.Items.Clear();
        foreach (var item in items)
            list.Items.Add(item.Station + "  " + item.SubFolderSummary);
        if (items.Count > 0)
            list.SelectedIndex = Math.Clamp(selected < 0 ? 0 : selected, 0, items.Count - 1);
        else
            list.ClearSelected();
    }

    private void ShowSelectedPath()
    {
        _pathBox.Text = SelectedItem()?.SaveFolder ?? "";
    }

    private StationTarget? SelectedItem()
    {
        if (_dispenseList.SelectedIndex >= 0 && _dispenseList.SelectedIndex < _dispense.Count)
            return _dispense[_dispenseList.SelectedIndex];
        if (_bindList.SelectedIndex >= 0 && _bindList.SelectedIndex < _bind.Count)
            return _bind[_bindList.SelectedIndex];
        return null;
    }

    private void AddItem(bool bind)
    {
        var draft = new StationTarget
        {
            Station = bind ? "0061" : "0031",
            SaveFolder = Config.DefaultSaveFolder(bind)
        };
        using var dialog = new StationTargetDialog(draft, bind, _watch.Text, Config.LookbackDays);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        (bind ? _bind : _dispense).Add(dialog.Target);
        RefreshLists();
        var list = bind ? _bindList : _dispenseList;
        list.SelectedIndex = list.Items.Count - 1;
    }

    private void EditSelected()
    {
        var bind = _bindList.SelectedIndex >= 0;
        var list = bind ? _bind : _dispense;
        var index = bind ? _bindList.SelectedIndex : _dispenseList.SelectedIndex;
        if (index < 0 || index >= list.Count)
            return;
        using var dialog = new StationTargetDialog(list[index], bind, _watch.Text, Config.LookbackDays);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        list[index] = dialog.Target;
        RefreshLists();
    }

    private void RemoveSelected()
    {
        if (_dispenseList.SelectedIndex >= 0)
        {
            _dispense.RemoveAt(_dispenseList.SelectedIndex);
            RefreshLists();
            return;
        }
        if (_bindList.SelectedIndex >= 0)
        {
            _bind.RemoveAt(_bindList.SelectedIndex);
            RefreshLists();
        }
    }

    private void Browse(TextBox target)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择目录",
            SelectedPath = Directory.Exists(target.Text) ? target.Text : ""
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            target.Text = dialog.SelectedPath;
    }

    private static Label MakeHeading(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = Color.FromArgb(232, 238, 247),
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold)
        };
    }

    private static ListBox MakeList(int x, int y, int w, int h)
    {
        return new ListBox
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = Color.FromArgb(10, 14, 24),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 12f)
        };
    }

    private static TextBox MakeTextBox(int x, int y, int width, string text)
    {
        return new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 32),
            Text = text,
            BackColor = Color.FromArgb(10, 14, 24),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 10f)
        };
    }

    private static NumericUpDown MakeNumber(int x, int y, int value, int min, int max)
    {
        return new NumericUpDown
        {
            Location = new Point(x, y),
            Size = new Size(120, 32),
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            BackColor = Color.FromArgb(232, 238, 247),
            ForeColor = Color.FromArgb(11, 16, 28),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 12f)
        };
    }

    private static Button MakeButton(string text, int x, int y, int width, Color color)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}

internal sealed class StationTargetDialog : Form
{
    private readonly string _watchRoot;
    private readonly int _lookbackDays;
    private readonly TextBox _station;
    private readonly TextBox _folder;
    private readonly CheckedListBox _subs;

    public StationTarget Target { get; private set; }

    public StationTargetDialog(StationTarget source, bool bind, string watchRoot, int lookbackDays)
    {
        Target = source.Clone();
        _watchRoot = watchRoot ?? "";
        _lookbackDays = Math.Max(1, lookbackDays);
        Text = bind ? "组装站" : "点胶站";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(860, 470);
        Branding.Apply(this);
        BackColor = Color.FromArgb(18, 26, 43);
        ForeColor = Color.FromArgb(232, 238, 247);
        Font = new Font("Microsoft YaHei UI", 10f);

        Controls.Add(MakeLabel("站位", 24, 28));
        _station = MakeTextBox(140, 24, 560, Target.Station);
        _station.PlaceholderText = bind ? "61 或 0061" : "31 或 0031";
        Controls.Add(_station);
        var refresh = MakeButton("刷新", 716, 22, 114);
        refresh.Click += (_, _) => RefreshSubFolders();
        Controls.Add(refresh);

        Controls.Add(MakeLabel("子目录", 24, 88));
        _subs = new CheckedListBox
        {
            Location = new Point(140, 84),
            Size = new Size(690, 180),
            BackColor = Color.FromArgb(10, 14, 24),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true,
            Font = new Font("Consolas", 11f)
        };
        Controls.Add(_subs);
        FillSubFolders(Target.SubFolders, Target.SubFolders);

        Controls.Add(MakeLabel("保存路径", 24, 286));
        _folder = MakeTextBox(140, 282, 560, Target.SaveFolder);
        var browse = MakeButton("选择目录", 716, 280, 114);
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择保存路径",
                SelectedPath = Directory.Exists(_folder.Text) ? _folder.Text : ""
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _folder.Text = dialog.SelectedPath;
        };
        Controls.Add(_folder);
        Controls.Add(browse);

        var cancel = MakeButton("取消", 624, 400, 96);
        cancel.BackColor = Color.FromArgb(42, 55, 78);
        cancel.DialogResult = DialogResult.Cancel;
        var save = MakeButton("保存", 734, 400, 96);
        save.BackColor = Color.FromArgb(61, 220, 255);
        save.ForeColor = Color.FromArgb(8, 16, 24);
        save.Click += (_, _) =>
        {
            Target.Station = _station.Text.Trim();
            Target.SaveFolder = _folder.Text.Trim();
            Target.SubFolders = SelectedSubFolders();
            if (Target.Station.Length == 0)
            {
                MessageBox.Show(this, "请填写站位。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (_subs.Items.Count > 0 && Target.SubFolders.Count == 0)
            {
                MessageBox.Show(this, "请勾选要提取的子目录。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (string.IsNullOrWhiteSpace(Target.SaveFolder))
            {
                MessageBox.Show(this, "请填写保存路径。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            Target.Normalize();
        };
        save.DialogResult = DialogResult.OK;
        Controls.Add(cancel);
        Controls.Add(save);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void RefreshSubFolders()
    {
        var station = _station.Text.Trim();
        if (station.Length == 0)
        {
            MessageBox.Show(this, "请先填写站位号。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_watchRoot))
        {
            MessageBox.Show(this, "请先在配置里填写报文目录。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Cursor = Cursors.WaitCursor;
        List<string> found;
        try
        {
            found = StationSubFolders.Discover(_watchRoot, station, _lookbackDays);
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show(this, "刷新失败：" + ex.Message, "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Cursor = Cursors.Default;

        var keep = SelectedSubFolders();
        if (keep.Count == 0)
            keep = Target.SubFolders;
        FillSubFolders(found, keep);
        if (found.Count == 0)
            MessageBox.Show(this, "未找到该站位的子目录。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void FillSubFolders(IReadOnlyList<string> items, IReadOnlyList<string> checkedItems)
    {
        var wanted = new HashSet<string>(StationSubFolders.NormalizeList(checkedItems), StringComparer.OrdinalIgnoreCase);
        _subs.Items.Clear();
        foreach (var item in items)
        {
            var key = StationSubFolders.NormalizeKey(item);
            if (key.Length == 0)
                continue;
            _subs.Items.Add(key, wanted.Contains(key));
        }
    }

    private List<string> SelectedSubFolders()
    {
        var list = new List<string>();
        for (var i = 0; i < _subs.Items.Count; i++)
        {
            if (!_subs.GetItemChecked(i))
                continue;
            list.Add(_subs.Items[i]?.ToString() ?? "");
        }
        return StationSubFolders.NormalizeList(list);
    }

    private static Label MakeLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = Color.FromArgb(232, 238, 247),
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold)
        };
    }

    private static TextBox MakeTextBox(int x, int y, int width, string text)
    {
        return new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 32),
            Text = text,
            BackColor = Color.FromArgb(10, 14, 24),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 10f)
        };
    }

    private static Button MakeButton(string text, int x, int y, int width)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 55, 78),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
