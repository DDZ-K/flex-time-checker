namespace FlexTimeChecker;

internal sealed class FolderProfile
{
    public string Name { get; set; } = "新目录";
    public string MessageFolder { get; set; } = "";
    public string Role { get; set; } = Roles.Dispense;
    public int DemolishMinutes { get; set; } = 120;
    public int ClearGlueMinutes { get; set; } = 240;

    public bool IsBind => string.Equals(Role, Roles.Bind, StringComparison.OrdinalIgnoreCase);
    public string RoleLabel => IsBind ? "组装站" : "点胶站";

    public FolderProfile Clone()
    {
        return new FolderProfile
        {
            Name = Name,
            MessageFolder = MessageFolder,
            Role = Role,
            DemolishMinutes = DemolishMinutes,
            ClearGlueMinutes = ClearGlueMinutes
        };
    }

    public void Normalize()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "未命名目录" : Name.Trim();
        MessageFolder = (MessageFolder ?? "").Trim();
        Role = Roles.Normalize(Role);
        DemolishMinutes = Math.Max(0, DemolishMinutes);
        ClearGlueMinutes = Math.Max(0, ClearGlueMinutes);
    }
}

internal static class Roles
{
    public const string Dispense = "Dispense";
    public const string Bind = "Bind";

    public static string Normalize(string? raw)
    {
        var value = (raw ?? "").Trim();
        if (value.Equals(Bind, StringComparison.OrdinalIgnoreCase)
            || value.Contains("组装", StringComparison.Ordinal)
            || value.Contains("绑定", StringComparison.Ordinal)
            || value.Equals("Assembly", StringComparison.OrdinalIgnoreCase))
            return Bind;
        return Dispense;
    }
}
