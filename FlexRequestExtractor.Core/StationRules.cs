namespace FlexRequestExtractor;

public static class StationRules
{
    public static bool Matches(string folderName, IReadOnlyList<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(folderName) || tokens is null || tokens.Count == 0)
            return false;

        foreach (var raw in tokens)
        {
            var token = (raw ?? "").Trim();
            if (token.Length == 0)
                continue;
            if (token == "*" || token.Equals("all", StringComparison.OrdinalIgnoreCase))
                return true;
            if (folderName.Equals(token, StringComparison.OrdinalIgnoreCase))
                return true;
            if (folderName.StartsWith(token + "_", StringComparison.OrdinalIgnoreCase))
                return true;
            if (int.TryParse(token, out var n))
            {
                var padded = n.ToString("D4");
                if (folderName.Equals(padded, StringComparison.OrdinalIgnoreCase)
                    || folderName.StartsWith(padded + "_", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
