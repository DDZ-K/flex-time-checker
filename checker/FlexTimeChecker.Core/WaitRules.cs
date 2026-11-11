namespace FlexTimeChecker.Core;
public sealed record Verdict(bool CanDemolish, bool CanClear, string? Error);
public static class WaitRules
{
    public static Verdict Evaluate(TimeSpan elapsed, int demolishMinutes, int clearMinutes) =>
        elapsed < TimeSpan.Zero ? new(false, false, "点胶时间晚于本机时间，禁止放行；请核对设备和本机时钟") :
        new(elapsed.TotalMinutes < demolishMinutes, elapsed.TotalMinutes > clearMinutes, null);
}
