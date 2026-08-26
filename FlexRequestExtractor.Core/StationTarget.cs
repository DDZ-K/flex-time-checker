namespace FlexRequestExtractor;

public sealed class StationTarget
{
    public string Station { get; set; } = "";
    public string SaveFolder { get; set; } = "";

    public StationTarget Clone() => new()
    {
        Station = Station,
        SaveFolder = SaveFolder
    };

    public void Normalize()
    {
        Station = (Station ?? "").Trim();
        SaveFolder = PathUtil.Normalize(SaveFolder);
    }
}
