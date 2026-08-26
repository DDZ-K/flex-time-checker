using AntdButton = AntdUI.Button;

namespace FlexTimeChecker;

internal sealed class FolderProfilesDialog : Form
{
    private static readonly Color Bg = Color.FromArgb(246, 248, 251);
    private static readonly Color Surface = Color.White;
    private static readonly Color Line = Color.FromArgb(226, 232, 240);
    private static readonly Color Ink = Color.FromArgb(15, 23, 42);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);

    private readonly ListBox _dispenseList;
    private readonly ListBox _bindList;
    private readonly TextBox _pathBox;
    private readonly List<FolderProfile> _profiles;

    public IReadOnlyList<FolderProfile> Profiles => _profiles;

    public FolderProfilesDialog(IEnumerable<FolderProfile> profiles)
    {
        _profiles = profiles.Select(x => x.Clone()).ToList();

        Text = "配置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(980, 540);
        Branding.Apply(this);
        BackColor = Bg;
        ForeColor = Ink;
        Font = new Font("Microsoft YaHei UI", 10f);

        Controls.Add(MakeHeading("点胶站", 20, 16));
        _dispenseList = MakeList(20, 48, 460, 300);
        Controls.Add(MakeHeading("组装站", 500, 16));
        _bindList = MakeList(500, 48, 460, 300);
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

        Controls.Add(MakeHeading("目录", 20, 360));
        _pathBox = new TextBox
        {
            Location = new Point(80, 356),
            Size = new Size(880, 30),
            ReadOnly = true,
            BackColor = Surface,
            ForeColor = Ink,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10f)
        };
        Controls.Add(_pathBox);

        var addDispense = MakeButton("新增点胶", 20, 412, 120, AntdUI.TTypeMini.Primary);
        addDispense.Click += (_, _) => AddProfile(Roles.Dispense);

        var addBind = MakeButton("新增组装", 150, 412, 120, AntdUI.TTypeMini.Primary);
        addBind.Click += (_, _) => AddProfile(Roles.Bind);

        var edit = MakeButton("编辑", 280, 412, 90, AntdUI.TTypeMini.Default);
        edit.Click += (_, _) => EditProfile();

        var remove = MakeButton("删除", 380, 412, 90, AntdUI.TTypeMini.Error);
        remove.Click += (_, _) => RemoveProfile();

        var cancel = MakeButton("取消", 750, 412, 90, AntdUI.TTypeMini.Default);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var save = MakeButton("保存", 850, 412, 90, AntdUI.TTypeMini.Primary);
        save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };

        Controls.Add(_dispenseList);
        Controls.Add(_bindList);
        Controls.Add(addDispense);
        Controls.Add(addBind);
        Controls.Add(edit);
        Controls.Add(remove);
        Controls.Add(cancel);
        Controls.Add(save);
        CancelButton = cancel;

        RefreshLists();
    }

    private IReadOnlyList<FolderProfile> DispenseItems() => _profiles.Where(x => !x.IsBind).ToList();
    private IReadOnlyList<FolderProfile> BindItems() => _profiles.Where(x => x.IsBind).ToList();

    private void RefreshLists()
    {
        Fill(_dispenseList, DispenseItems(), false);
        Fill(_bindList, BindItems(), true);
        ShowSelectedPath();
    }

    private static void Fill(ListBox list, IReadOnlyList<FolderProfile> items, bool bind)
    {
        var selected = list.SelectedIndex;
        list.Items.Clear();
        foreach (var item in items)
        {
            list.Items.Add(bind
                ? item.Name
                : $"{item.Name}    拆除 {item.DemolishMinutes} 分钟    清胶 {item.ClearGlueMinutes} 分钟");
        }
        if (items.Count > 0)
            list.SelectedIndex = Math.Clamp(selected < 0 ? 0 : selected, 0, items.Count - 1);
        else
            list.ClearSelected();
    }

    private void ShowSelectedPath()
    {
        var current = SelectedProfile();
        _pathBox.Text = current?.MessageFolder ?? "";
        _pathBox.SelectionStart = 0;
        _pathBox.SelectionLength = 0;
    }

    private FolderProfile? SelectedProfile()
    {
        if (_dispenseList.SelectedIndex >= 0)
        {
            var items = DispenseItems();
            return _dispenseList.SelectedIndex < items.Count ? items[_dispenseList.SelectedIndex] : null;
        }
        if (_bindList.SelectedIndex >= 0)
        {
            var items = BindItems();
            return _bindList.SelectedIndex < items.Count ? items[_bindList.SelectedIndex] : null;
        }
        return null;
    }

    private void AddProfile(string role)
    {
        using var dialog = new FolderProfileEditor(new FolderProfile { Role = role });
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _profiles.Add(dialog.Profile);
        RefreshLists();
    }

    private void EditProfile()
    {
        var current = SelectedProfile();
        if (current is null)
            return;
        var index = _profiles.IndexOf(current);
        using var dialog = new FolderProfileEditor(current);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _profiles[index] = dialog.Profile;
        RefreshLists();
    }

    private void RemoveProfile()
    {
        var current = SelectedProfile();
        if (current is null)
            return;
        _profiles.Remove(current);
        RefreshLists();
    }

    private static Label MakeHeading(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = Ink,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold)
        };
    }

    private static ListBox MakeList(int x, int y, int w, int h)
    {
        return new ListBox
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = Surface,
            ForeColor = Ink,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 11f)
        };
    }

    private static AntdButton MakeButton(string text, int x, int y, int width, AntdUI.TTypeMini type)
    {
        var button = new AntdButton
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 38),
            Type = type,
            Radius = 6,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
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

internal sealed class FolderProfileEditor : Form
{
    private const int FieldX = 128;
    private static readonly Color Bg = Color.FromArgb(246, 248, 251);
    private static readonly Color Surface = Color.White;
    private static readonly Color Line = Color.FromArgb(226, 232, 240);
    private static readonly Color Ink = Color.FromArgb(15, 23, 42);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);

    private readonly TextBox _name;
    private readonly TextBox _folder;
    private readonly ComboBox _role;
    private readonly NumericUpDown _demolish;
    private readonly NumericUpDown _clearGlue;
    private readonly Label _demolishLabel;
    private readonly Label _clearLabel;

    public FolderProfile Profile => new()
    {
        Name = _name.Text.Trim(),
        MessageFolder = _folder.Text.Trim(),
        Role = _role.SelectedIndex == 1 ? Roles.Bind : Roles.Dispense,
        DemolishMinutes = (int)_demolish.Value,
        ClearGlueMinutes = (int)_clearGlue.Value
    };

    public FolderProfileEditor(FolderProfile source)
    {
        Text = "目录配置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(780, 360);
        Branding.Apply(this);
        BackColor = Bg;
        ForeColor = Ink;
        Font = new Font("Microsoft YaHei UI", 10f);

        Controls.Add(MakeLabel("名称", 24, 28));
        _name = MakeTextBox(FieldX, 24, 620, source.Name);
        Controls.Add(_name);

        Controls.Add(MakeLabel("类型", 24, 82));
        _role = new ComboBox
        {
            Location = new Point(FieldX, 78),
            Size = new Size(220, 32),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Surface,
            ForeColor = Ink,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 11f)
        };
        _role.Items.Add("点胶站");
        _role.Items.Add("组装站");
        _role.SelectedIndex = source.IsBind ? 1 : 0;
        _role.SelectedIndexChanged += (_, _) => ToggleThresholds();
        Controls.Add(_role);

        Controls.Add(MakeLabel("报文目录", 24, 136));
        _folder = MakeTextBox(FieldX, 132, 488, source.MessageFolder);
        var browse = MakeButton("选择目录", 628, 130, 120, AntdUI.TTypeMini.Default);
        browse.Click += (_, _) => BrowseFolder();
        Controls.Add(_folder);
        Controls.Add(browse);

        _demolishLabel = MakeLabel("拆除分钟", 24, 196);
        Controls.Add(_demolishLabel);
        _demolish = MakeNumber(FieldX, 192, source.DemolishMinutes);
        Controls.Add(_demolish);

        _clearLabel = MakeLabel("清胶分钟", 320, 196);
        Controls.Add(_clearLabel);
        _clearGlue = MakeNumber(428, 192, source.ClearGlueMinutes);
        Controls.Add(_clearGlue);

        var cancel = MakeButton("取消", 508, 286, 110, AntdUI.TTypeMini.Default);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var save = MakeButton("保存", 628, 286, 120, AntdUI.TTypeMini.Primary);
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_folder.Text))
            {
                MessageBox.Show(this, "请选择报文目录。", "配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_name.Text))
                _name.Text = Path.GetFileName(_folder.Text.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(cancel);
        Controls.Add(save);
        ToggleThresholds();
    }

    private void ToggleThresholds()
    {
        var enable = _role.SelectedIndex != 1;
        _demolish.Enabled = enable;
        _clearGlue.Enabled = enable;
        _demolishLabel.ForeColor = enable ? Ink : Muted;
        _clearLabel.ForeColor = enable ? Ink : Muted;
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择只包含 Request 报文的目录",
            SelectedPath = Directory.Exists(_folder.Text) ? _folder.Text : ""
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folder.Text = dialog.SelectedPath;
            _folder.SelectionStart = 0;
            _folder.SelectionLength = 0;
        }
    }

    private static Label MakeLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = Ink,
            Font = new Font("Microsoft YaHei UI", 11f)
        };
    }

    private static TextBox MakeTextBox(int x, int y, int width, string text)
    {
        var box = new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 32),
            Text = text,
            BackColor = Surface,
            ForeColor = Ink,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 10f)
        };
        box.SelectionStart = 0;
        box.SelectionLength = 0;
        return box;
    }

    private static NumericUpDown MakeNumber(int x, int y, int value)
    {
        return new NumericUpDown
        {
            Location = new Point(x, y),
            Size = new Size(140, 32),
            Minimum = 0,
            Maximum = 100000,
            Value = Math.Clamp(value, 0, 100000),
            BackColor = Surface,
            ForeColor = Ink,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 12f)
        };
    }

    private static AntdButton MakeButton(string text, int x, int y, int width, AntdUI.TTypeMini type)
    {
        var button = new AntdButton
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 36),
            Type = type,
            Radius = 6,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
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
