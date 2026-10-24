using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RimeNotice.Services;

// Explicit, bounded local sampling only. Producers never perform file I/O or wait.
public static class DiagnosticCapture
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };
    private static readonly object Gate = new();
    private static Session? _current;
    public static bool Active => Volatile.Read(ref _current)?.Accepting == true;
    public static string LastPath { get; private set; } = "";
    public static string Status { get; private set; } = "未采样";
    public static string Text(string value) => value.Length <= 32768 ? value : value[..32768];

    public static string Start()
    {
        lock (Gate)
        {
            if (_current?.Accepting == true) return LastPath;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RimeNotice", "Diagnostics");
            Directory.CreateDirectory(folder);
            var session = new Session(Path.Combine(folder, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jsonl"));
            LastPath = session.Path;
            _current = session;
            Status = "采样中（最多 5 分钟／16 MiB；仅本地，含测试消息原文）";
            session.Worker = Task.Run(session.RunAsync);
            session.Write("capture.start", new { schema = 1, maxSeconds = 300, maxBytes = 16 * 1024 * 1024, textLimit = 32768, process = Environment.ProcessId, version = typeof(DiagnosticCapture).Assembly.GetName().Version?.ToString(), utcOffset = DateTimeOffset.Now.Offset.ToString() });
            session.Timeout = new Timer(_ => session.Complete("duration-limit"), null, TimeSpan.FromMinutes(5), System.Threading.Timeout.InfiniteTimeSpan);
            return LastPath;
        }
    }

    public static async Task StopAsync()
    {
        Session? session;
        lock (Gate) { session = _current; session?.Complete("manual-stop"); }
        if (session is not null) await session.Worker;
    }

    public static void Write(string kind, object data)
    {
        try { Volatile.Read(ref _current)?.Write(kind, data); }
        catch (Exception) { /* Diagnostics must not affect notification delivery. */ }
    }

    private sealed record Row(long Sequence, DateTimeOffset Utc, double ElapsedMs, string Kind, object Data);
    private sealed class Session(string path)
    {
        private readonly Channel<Row> _rows = Channel.CreateBounded<Row>(new BoundedChannelOptions(1024) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _sequence, _dropped;
        private int _accepting = 1;
        private string _reason = "manual-stop";
        public string Path { get; } = path;
        public bool Accepting => Volatile.Read(ref _accepting) == 1;
        public Task Worker { get; set; } = Task.CompletedTask;
        public Timer? Timeout { get; set; }
        public void Write(string kind, object data)
        {
            if (!Accepting) return;
            var row = new Row(Interlocked.Increment(ref _sequence), DateTimeOffset.UtcNow, _clock.Elapsed.TotalMilliseconds, kind, data);
            if (!_rows.Writer.TryWrite(row)) Interlocked.Increment(ref _dropped);
        }
        public void Complete(string reason)
        {
            if (Interlocked.Exchange(ref _accepting, 0) == 0) return;
            _reason = reason;
            Timeout?.Dispose();
            _rows.Writer.TryComplete();
        }
        public async Task RunAsync()
        {
            long bytes = 0;
            try
            {
                await using var file = new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 16384, FileOptions.Asynchronous);
                await using var writer = new StreamWriter(file, new UTF8Encoding(false));
                await foreach (var row in _rows.Reader.ReadAllAsync())
                {
                    string json = JsonSerializer.Serialize(row, JsonOptions);
                    int count = Encoding.UTF8.GetByteCount(json) + 1;
                    if (bytes + count > 16 * 1024 * 1024 - 2048)
                    {
                        Interlocked.Increment(ref _dropped);
                        // A queued oversized row can be consumed after a manual stop.
                        // Preserve the actual truncation reason in the final summary.
                        _reason = "size-limit";
                        Complete("size-limit");
                        while (_rows.Reader.TryRead(out _)) Interlocked.Increment(ref _dropped);
                        break;
                    }
                    await writer.WriteLineAsync(json);
                    bytes += count;
                }
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { Kind = "capture.stop", Utc = DateTimeOffset.UtcNow, ElapsedMs = _clock.Elapsed.TotalMilliseconds, reason = _reason, droppedRecords = Interlocked.Read(ref _dropped), bytesBeforeSummary = bytes }));
                await writer.FlushAsync();
                lock (Gate) { if (ReferenceEquals(_current, this)) Status = $"已停止（{_reason}）；丢弃记录 {_dropped} 条"; }
            }
            catch (Exception ex)
            {
                Complete("write-failed");
                lock (Gate) { if (ReferenceEquals(_current, this)) Status = "采样写入失败：" + ex.GetType().Name; }
                ServiceDiagnostics.Error("诊断采样", ex);
            }
        }
    }
}
