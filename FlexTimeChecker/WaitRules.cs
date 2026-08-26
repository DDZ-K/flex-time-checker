namespace FlexTimeChecker;

internal static class WaitRules
{
    public static bool CanDemolish(TimeSpan elapsed, int demolishMinutes)
        => elapsed.TotalMinutes < Math.Max(0, demolishMinutes);

    public static bool CanClearGlue(TimeSpan elapsed, int clearGlueMinutes)
        => elapsed.TotalMinutes > Math.Max(0, clearGlueMinutes);
}
