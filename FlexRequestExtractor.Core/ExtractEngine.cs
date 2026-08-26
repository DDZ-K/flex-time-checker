using System.Collections.Concurrent;

namespace FlexRequestExtractor;

public sealed class ScanSnapshot
{
    public int Copied { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public int Deleted { get; init; }
    public List<string> CopiedNames { get; init; } = [];
    public List<string> DeletedNames { get; init; } = [];
    public string? Error { get; init; }
}

public sealed class ExtractEngine : IDisposable
{
    public const string MutexName = @"Global\FlexRequestExtractor.Engine";

    private readonly AppConfig _cfg;
    private readonly ConcurrentDictionary<string, long> _handled = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private Mutex? _mutex;
    private DateTime _sessionStart = DateTime.Now;
    private int _copied;
    private int _skipped;
    private int _failed;
    private int _deleted;

    public ExtractEngine(AppConfig cfg) => _cfg = cfg;

    public bool IsRunning { get; private set; }
    public int Copied => _copied;
    public int Skipped => _skipped;
    public int Failed => _failed;
    public int Deleted => _deleted;
    public string LastFile { get; private set; } = "";
    public string LastStation { get; private set; } = "";
    public string LastError { get; private set; } = "";

    public event Action<string>? Logged;
    public event Action? StatsChanged;

    public void PrepareSession()
    {
        _handled.Clear();
        _copied = 0;
        _skipped = 0;
        _failed = 0;
        _deleted = 0;
        LastFile = "";
        LastStation = "";
        LastError = "";
        _sessionStart = DateTime.Now;
    }

    public bool Start()
    {
        lock (_gate)
        {
            if (IsRunning)
                return true;
            if (!TryTakeMutex())
            {
                Logged?.Invoke("已有提取进程在运行（界面或服务只可一个）");
                return false;
            }
            PrepareSession();
            _cts = new CancellationTokenSource();
            IsRunning = true;
            var token = _cts.Token;
            _worker = Task.Factory.StartNew(() => Worker(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        Logged?.Invoke($"开始监控  保留{_cfg.KeepDays}天");
        StatsChanged?.Invoke();
        return true;
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? worker;
        lock (_gate)
        {
            if (!IsRunning)
            {
                ReleaseMutex();
                return;
            }
            IsRunning = false;
            cts = _cts;
            worker = _worker;
            _cts = null;
            _worker = null;
        }
        try { cts?.Cancel(); } catch { }
        try { worker?.Wait(2000); } catch { }
        cts?.Dispose();
        ReleaseMutex();
        Logged?.Invoke("已停止");
        StatsChanged?.Invoke();
    }

    private void Worker(CancellationToken token)
    {
        string? lastError = null;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var snap = ScanOnce();
                if (!string.IsNullOrWhiteSpace(snap.Error))
                {
                    LastError = snap.Error;
                    if (!string.Equals(snap.Error, lastError, StringComparison.Ordinal))
                    {
                        Logged?.Invoke(snap.Error);
                        lastError = snap.Error;
                    }
                }
                else
                {
                    LastError = "";
                    lastError = null;
                }
            }
            catch (Exception ex)
            {
                Logged?.Invoke("扫描失败 " + ex.Message);
            }

            var waitMs = Math.Max(1, _cfg.PollIntervalSec) * 1000;
            var until = DateTime.UtcNow.AddMilliseconds(waitMs);
            while (!token.IsCancellationRequested && DateTime.UtcNow < until)
                Thread.Sleep(100);
        }
    }

    public ScanSnapshot ScanOnce()
    {
        var copiedNames = new List<string>();
        var watch = PathUtil.Normalize(_cfg.WatchRoot);
        if (string.IsNullOrWhiteSpace(watch))
            return new ScanSnapshot { Error = "未配置报文目录" };
        if (_cfg.DispenseItems.Count == 0 && _cfg.BindItems.Count == 0)
            return new ScanSnapshot { Error = "未配置点胶站或组装站" };
        if (_cfg.AllSaveFolders.Count == 0)
            return new ScanSnapshot { Error = "未配置保存路径" };
        if (!Directory.Exists(watch))
            return new ScanSnapshot { Error = "报文目录不存在或无法访问" };

        foreach (var folder in _cfg.AllSaveFolders)
        {
            try { Directory.CreateDirectory(folder); }
            catch { }
        }

        foreach (var dateDir in EnumerateDateFolders(watch))
        {
            IEnumerable<string> stations;
            try { stations = Directory.EnumerateDirectories(dateDir); }
            catch { continue; }

            foreach (var stationDir in stations)
            {
                string stationName;
                try { stationName = Path.GetFileName(stationDir.TrimEnd('\\')); }
                catch { continue; }
                var dispenseHits = _cfg.MatchDispense(stationName).ToList();
                var bindHits = _cfg.MatchBind(stationName).ToList();
                if (dispenseHits.Count == 0 && bindHits.Count == 0)
                    continue;

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(stationDir, "Request*.xml", SearchOption.AllDirectories);
                }
                catch { continue; }

                foreach (var file in files)
                {
                    foreach (var item in dispenseHits)
                    {
                        if (!StationSubFolders.FileMatches(stationDir, file, item.SubFolders))
                            continue;
                        TryCopy(file, stationName, AppConfig.DispenseFolderName, item.SaveFolder, copiedNames);
                    }
                    foreach (var item in bindHits)
                    {
                        if (!StationSubFolders.FileMatches(stationDir, file, item.SubFolders))
                            continue;
                        TryCopy(file, stationName, AppConfig.BindFolderName, item.SaveFolder, copiedNames);
                    }
                }
            }
        }

        var deletedNames = PurgeExpired();
        return new ScanSnapshot
        {
            Copied = _copied,
            Skipped = _skipped,
            Failed = _failed,
            Deleted = _deleted,
            CopiedNames = copiedNames,
            DeletedNames = deletedNames
        };
    }

    public List<string> PurgeExpired()
    {
        var deleted = new List<string>();
        foreach (var folder in _cfg.AllSaveFolders)
            deleted.AddRange(PurgeOneFolder(folder));
        if (deleted.Count > 0)
            StatsChanged?.Invoke();
        return deleted;
    }

    private List<string> PurgeOneFolder(string output)
    {
        var deleted = new List<string>();
        if (string.IsNullOrWhiteSpace(output) || !Directory.Exists(output))
            return deleted;
        var cutoff = RetentionCutoff();
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(output, "Request*.xml", SearchOption.AllDirectories);
        }
        catch
        {
            return deleted;
        }

        foreach (var file in files)
        {
            FileInfo info;
            try { info = new FileInfo(file); }
            catch { continue; }
            if (!info.Exists)
                continue;
            if (!IsOlderThanCutoff(info, cutoff))
                continue;
            try
            {
                File.Delete(file);
                deleted.Add(info.Name);
                Interlocked.Increment(ref _deleted);
                Logged?.Invoke($"删除过期 {info.Name}");
            }
            catch
            {
            }
        }

        return deleted;
    }

    private IEnumerable<string> EnumerateDateFolders(string root)
    {
        var lookback = Math.Max(1, Math.Max(_cfg.LookbackDays, _cfg.KeepDays + 1));
        var found = false;
        for (var i = 0; i < lookback; i++)
        {
            var day = DateTime.Now.Date.AddDays(-i);
            var path = Path.Combine(root, day.ToString("yyyy"), day.ToString("MM"), day.ToString("dd"));
            if (Directory.Exists(path))
            {
                found = true;
                yield return path;
            }
        }

        if (!found)
            yield return root;
    }

    private void TryCopy(string source, string station, string role, string destDir, List<string> copiedNames)
    {
        FileInfo info;
        try { info = new FileInfo(source); }
        catch { return; }
        if (!info.Exists)
            return;

        var name = info.Name;
        if (!name.StartsWith("Request", StringComparison.OrdinalIgnoreCase)
            || !name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return;

        long ticks;
        try { ticks = info.LastWriteTimeUtc.Ticks; }
        catch { return; }

        var key = source + "|" + destDir;
        if (_handled.TryGetValue(key, out var prev) && prev >= ticks)
            return;

        if (IsOlderThanCutoff(info, RetentionCutoff()))
        {
            _handled[key] = ticks;
            Interlocked.Increment(ref _skipped);
            return;
        }

        if (_cfg.OnlyNewAfterStart)
        {
            DateTime ctime, mtime;
            try
            {
                ctime = info.CreationTime;
                mtime = info.LastWriteTime;
            }
            catch { return; }
            var cutoff = _sessionStart - TimeSpan.FromSeconds(Math.Max(0, _cfg.NewFileGraceSec));
            if (ctime < cutoff && mtime < cutoff)
            {
                _handled[key] = ticks;
                Interlocked.Increment(ref _skipped);
                return;
            }
        }

        long length;
        try { length = info.Length; }
        catch { return; }
        if (length < _cfg.MinFileBytes)
            return;
        if (!IsSizeStable(info))
            return;

        if (string.IsNullOrWhiteSpace(destDir))
            return;
        var dest = Path.Combine(destDir, name);
        try
        {
            if (File.Exists(dest))
            {
                var destLen = new FileInfo(dest).Length;
                if (destLen == length)
                {
                    _handled[key] = ticks;
                    Interlocked.Increment(ref _skipped);
                    return;
                }
            }
        }
        catch { }

        try
        {
            Directory.CreateDirectory(destDir);
            CopyShared(source, dest);
            try
            {
                File.SetLastWriteTime(dest, info.LastWriteTime);
                File.SetCreationTime(dest, info.CreationTime);
            }
            catch { }
            _handled[key] = ticks;
            Interlocked.Increment(ref _copied);
            LastFile = name;
            LastStation = station;
            copiedNames.Add(name);
            Logged?.Invoke($"复制 {name}  ← {role} {station}");
            StatsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failed);
            Logged?.Invoke($"失败 {name}  {ex.Message}");
            StatsChanged?.Invoke();
        }
    }

    private DateTime RetentionCutoff() => DateTime.Now - TimeSpan.FromDays(Math.Max(1, _cfg.KeepDays));

    private static bool IsOlderThanCutoff(FileInfo info, DateTime cutoff)
    {
        try
        {
            var newest = info.CreationTime > info.LastWriteTime ? info.CreationTime : info.LastWriteTime;
            return newest < cutoff;
        }
        catch
        {
            return false;
        }
    }

    private bool IsSizeStable(FileInfo info)
    {
        var checks = _cfg.SizeStableChecks;
        if (checks <= 0)
            return true;
        try
        {
            var last = info.Length;
            for (var i = 0; i < checks; i++)
            {
                if (_cfg.SizeStableIntervalMs > 0)
                    Thread.Sleep(_cfg.SizeStableIntervalMs);
                info.Refresh();
                if (!info.Exists || info.Length != last)
                    return false;
                last = info.Length;
            }
            return last >= _cfg.MinFileBytes;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyShared(string source, string dest)
    {
        using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
        src.CopyTo(dst);
    }

    private bool TryTakeMutex()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out var created);
            if (created)
                return true;
            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        catch
        {
            try
            {
                _mutex = new Mutex(true, @"Local\FlexRequestExtractor.Engine", out var created);
                if (created)
                    return true;
                _mutex.Dispose();
                _mutex = null;
                return false;
            }
            catch
            {
                return true;
            }
        }
    }

    private void ReleaseMutex()
    {
        try { _mutex?.ReleaseMutex(); } catch { }
        try { _mutex?.Dispose(); } catch { }
        _mutex = null;
    }

    public void Dispose() => Stop();
}
