using FlexRequestExtractor;
using Microsoft.Extensions.Hosting;

namespace FlexRequestExtractor.Service;

internal sealed class Worker : BackgroundService
{
    private static readonly object LogGate = new();
    private static int _logWrites;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WriteLog("service start");
        while (!stoppingToken.IsCancellationRequested)
        {
            AppConfig cfg;
            try
            {
                cfg = AppConfig.Load();
            }
            catch (Exception ex)
            {
                WriteLog("load config failed " + ex.Message);
                await Delay(stoppingToken, 5000);
                continue;
            }

            if (string.IsNullOrWhiteSpace(cfg.WatchRoot)
                || (cfg.DispenseItems.Count == 0 && cfg.BindItems.Count == 0))
            {
                WriteLog("config incomplete, retry 5s");
                await Delay(stoppingToken, 5000);
                continue;
            }

            using var engine = new ExtractEngine(cfg);
            engine.Logged += WriteLog;
            if (!engine.Start())
            {
                WriteLog("engine not started, retry 5s");
                await Delay(stoppingToken, 5000);
                continue;
            }

            try
            {
                while (!stoppingToken.IsCancellationRequested && engine.IsRunning)
                    await Delay(stoppingToken, 1000);
            }
            finally
            {
                engine.Stop();
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                WriteLog("engine stopped, retry 5s");
                await Delay(stoppingToken, 5000);
            }
        }
        WriteLog("service stop");
    }

    private static async Task Delay(CancellationToken token, int ms)
    {
        try { await Task.Delay(ms, token); }
        catch (OperationCanceledException) { }
    }

    private static void WriteLog(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
        try { Console.WriteLine(line); } catch { }
        try
        {
            var dir = Path.Combine(AppConfig.AppDir, "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "service.log");
            lock (LogGate)
            {
                File.AppendAllText(path, line + Environment.NewLine);
                _logWrites++;
                if (_logWrites % 20 != 0)
                    return;
                var lines = File.ReadAllLines(path);
                if (lines.Length > 500)
                    File.WriteAllLines(path, lines.Skip(lines.Length - 500));
            }
        }
        catch
        {
        }
    }
}
