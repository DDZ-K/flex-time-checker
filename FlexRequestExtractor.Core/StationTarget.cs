namespace FlexRequestExtractor;

public sealed class StationTarget
{
    public string Station { get; set; } = "";
    public string SaveFolder { get; set; } = "";
    public List<string> SubFolders { get; set; } = [];

    public StationTarget Clone() => new()
    {
        Station = Station,
        SaveFolder = SaveFolder,
        SubFolders = StationSubFolders.NormalizeList(SubFolders)
    };

    public void Normalize()
    {
        Station = (Station ?? "").Trim();
        SaveFolder = PathUtil.Normalize(SaveFolder);
        SubFolders = StationSubFolders.NormalizeList(SubFolders);
    }

    public string SubFolderSummary =>
        SubFolders.Count == 0 ? "全部子目录" : string.Join("、", SubFolders);
}
