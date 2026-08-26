namespace FlexRequestExtractor;

public static class PathUtil
{
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var s = raw.Trim().Replace('/', '\\');
        if (s.StartsWith('\\') && !s.StartsWith(@"\\", StringComparison.Ordinal))
            s = "\\" + s;

        if (s.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var body = s[2..];
            while (body.Contains(@"\\", StringComparison.Ordinal))
                body = body.Replace(@"\\", @"\", StringComparison.Ordinal);
            return @"\\" + body.TrimEnd('\\');
        }

        try
        {
            return Path.GetFullPath(s);
        }
        catch
        {
            return s;
        }
    }
}
