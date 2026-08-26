using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml;

namespace FlexTimeChecker;

internal static class RequestXmlSearch
{
    public static SearchResult Find(string scannedCode, AppConfig config)
    {
        var sw = Stopwatch.StartNew();
        scannedCode = (scannedCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(scannedCode))
            return new SearchResult { Error = "扫码为空", ElapsedMs = sw.ElapsedMilliseconds };

        var dispense = config.GetDispenseProfiles();
        var bind = config.GetBindProfiles();
        if (dispense.Count == 0 && bind.Count == 0)
            return new SearchResult { Error = "未配置报文目录，请点左上角“配置”", ElapsedMs = sw.ElapsedMilliseconds };

        var scanned = 0;
        var missing = new List<string>();

        var housingHit = FindHousing(scannedCode, dispense, missing, ref scanned, sw);
        if (housingHit != null)
            return housingHit;

        if (bind.Count == 0)
        {
            return NotFound(
                $"未在点胶站 Request*.xml 中找到 {scannedCode}。若这是 PCB/Cover 物料码，请先配置组装站目录。{MissingSuffix(missing)}",
                scanned, sw);
        }

        var bindHit = FindMaterial(scannedCode, bind, missing, ref scanned, sw);
        if (bindHit is null)
        {
            return NotFound(
                $"未找到 {scannedCode}（点胶站 Housing 与组装站物料均未命中）{MissingSuffix(missing)}",
                scanned, sw);
        }

        var dispenseHit = FindHousing(bindHit.HousingCode!, dispense, missing, ref scanned, sw);
        if (dispenseHit is null)
        {
            return new SearchResult
            {
                Error = $"物料 {scannedCode} 已绑定 Housing {bindHit.HousingCode}，但点胶站未找到该 Housing 的点胶时间{MissingSuffix(missing)}",
                HousingCode = bindHit.HousingCode,
                MaterialCode = scannedCode,
                BindFilePath = bindHit.BindFilePath,
                FilesScanned = scanned,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }

        return new SearchResult
        {
            Found = true,
            FilePath = dispenseHit.FilePath,
            BindFilePath = bindHit.BindFilePath,
            DispenseTime = dispenseHit.DispenseTime,
            HousingCode = bindHit.HousingCode,
            MaterialCode = scannedCode,
            Profile = bindHit.Profile ?? dispenseHit.Profile,
            FilesScanned = scanned,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    private static SearchResult? FindHousing(
        string housingCode,
        IReadOnlyList<FolderProfile> profiles,
        List<string> missing,
        ref int scanned,
        Stopwatch sw)
    {
        SearchResult? best = null;
        foreach (var profile in profiles)
        {
            if (!Directory.Exists(profile.MessageFolder))
            {
                missing.Add(profile.Name);
                continue;
            }

            var found = ScanFolder(profile, housingCode, matchMaterial: false, out var scannedHere);
            scanned += scannedHere;
            if (found is null)
                continue;
            if (best is null || found.DispenseTime > best.DispenseTime)
                best = found with { ElapsedMs = sw.ElapsedMilliseconds, FilesScanned = scanned };
        }
        return best;
    }

    private static SearchResult? FindMaterial(
        string materialCode,
        IReadOnlyList<FolderProfile> profiles,
        List<string> missing,
        ref int scanned,
        Stopwatch sw)
    {
        SearchResult? best = null;
        foreach (var profile in profiles)
        {
            if (!Directory.Exists(profile.MessageFolder))
            {
                missing.Add(profile.Name);
                continue;
            }

            var found = ScanFolder(profile, materialCode, matchMaterial: true, out var scannedHere);
            scanned += scannedHere;
            if (found is null)
                continue;
            if (best is null || found.DispenseTime > best.DispenseTime)
                best = found with { ElapsedMs = sw.ElapsedMilliseconds, FilesScanned = scanned };
        }
        return best;
    }

    private static SearchResult? ScanFolder(FolderProfile profile, string code, bool matchMaterial, out int scanned)
    {
        scanned = 0;
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(profile.MessageFolder, "Request*.xml", SearchOption.AllDirectories);
        }
        catch
        {
            return null;
        }

        var hits = new ConcurrentBag<(string path, string housing, DateTimeOffset time)>();
        var scannedHere = 0;
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8)
        };

        try
        {
            Parallel.ForEach(files, options, path =>
            {
                Interlocked.Increment(ref scannedHere);
                if (!TryReadRequest(path, out var housing, out var items, out var stamp))
                    return;
                if (matchMaterial)
                {
                    if (!items.Any(x => x.Equals(code, StringComparison.OrdinalIgnoreCase)))
                        return;
                    if (string.IsNullOrWhiteSpace(housing))
                        return;
                }
                else if (!housing.Equals(code, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                hits.Add((path, housing, stamp));
            });
        }
        catch (UnauthorizedAccessException)
        {
            scanned = scannedHere;
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            scanned = scannedHere;
            return null;
        }

        scanned = scannedHere;
        if (hits.IsEmpty)
            return null;

        var best = hits.MaxBy(x => x.time);
        return new SearchResult
        {
            Found = !matchMaterial,
            FilePath = matchMaterial ? null : best.path,
            BindFilePath = matchMaterial ? best.path : null,
            DispenseTime = best.time.LocalDateTime,
            HousingCode = best.housing,
            MaterialCode = matchMaterial ? code : null,
            Profile = profile,
            FilesScanned = scannedHere
        };
    }

    internal static bool TryReadRequest(
        string path,
        out string housing,
        out List<string> itemIds,
        out DateTimeOffset stamp)
    {
        housing = "";
        itemIds = [];
        stamp = default;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                IgnoreProcessingInstructions = true,
                CloseInput = true
            };
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                if (reader.Name == "header")
                {
                    var raw = reader.GetAttribute("timeStamp");
                    if (!DateTimeOffset.TryParse(raw, out stamp))
                        return false;
                }
                else if (reader.Name == "partProcessed")
                {
                    housing = reader.GetAttribute("identifier")?.Trim() ?? "";
                }
                else if (reader.Name == "item")
                {
                    var id = reader.GetAttribute("identifier")?.Trim();
                    if (!string.IsNullOrWhiteSpace(id))
                        itemIds.Add(id);
                }
            }
            return housing.Length > 0 && stamp != default;
        }
        catch
        {
            return false;
        }
    }

    private static SearchResult NotFound(string error, int scanned, Stopwatch sw)
    {
        return new SearchResult
        {
            Error = error,
            FilesScanned = scanned,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    private static string MissingSuffix(List<string> missing)
        => missing.Count == 0 ? "" : $"；不可用目录：{string.Join("、", missing)}";
}
