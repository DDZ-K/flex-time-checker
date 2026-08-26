using System.Reflection;

namespace FlexTimeChecker;

internal static class Branding
{
    public static Image? Logo { get; } = LoadPng("bosch-logo.png");
    public static Icon? AppIcon { get; } = LoadIco("bosch.ico");

    public static void Apply(Form form)
    {
        if (AppIcon is not null)
            form.Icon = AppIcon;
    }

    public static Control? MakeHeaderLogo(int x, int y)
    {
        if (Logo is null)
            return null;
        return new PictureBox
        {
            Image = Logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(x, y),
            Size = new Size(214, 40),
            BackColor = Color.Transparent
        };
    }

    private static Image? LoadPng(string file)
    {
        using var stream = Open(file);
        if (stream is null)
            return null;
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static Icon? LoadIco(string file)
    {
        using var stream = Open(file);
        if (stream is null)
            return null;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    private static Stream? Open(string file)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(file, StringComparison.OrdinalIgnoreCase));
        return name is null ? null : asm.GetManifestResourceStream(name);
    }
}
