using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Graviton;

using NSubstitute;

using Playnite;

using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

using Xunit;

namespace Graviton.Tests.Fakes.Saves;

internal sealed class SaveWatcherTestHarness : IDisposable
{
    private static readonly FieldInfo WatchersField = typeof(SaveWatcher).GetField("FileWatchers", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo ChangedMethod = typeof(SaveWatcher).GetMethod("OnChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ConcurrentQueue<int> _captureRequests = new();
    private readonly GravitonContextFake _plugin = new();
    private TaskCompletionSource<bool> _captureOccurred = NewSignal();

    public string Root { get; }
    public SaveWatcher Watcher { get; }
    public DateTime CurrentTime { get; set; } = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    public byte[]? Screenshot { get; set; } = new byte[] { 0xA1, 0xB2 };
    public bool FailCapture { get; set; }
    public int CaptureCount => _captureRequests.Count;
    public int[] CaptureRequests => _captureRequests.ToArray();
    public List<FileSystemWatcher> FileWatchers => (List<FileSystemWatcher>)WatchersField.GetValue(Watcher)!;
    public int SecondsBeforeSave
    {
        get { return _plugin.Settings.SecondsBeforeSave; }
        set { _plugin.Settings.SecondsBeforeSave = value; }
    }

    public SaveWatcherTestHarness()
    {
        Root = Path.Combine(Path.GetTempPath(), "GravitonSaveWatcherTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        var playniteAPI = Substitute.For<IPlayniteApi>();
        var logger = new GravitonLogger(_plugin.Settings);
        Watcher = new SaveWatcher(_plugin.Context, playniteAPI, logger, Capture, () => CurrentTime);
    }

    public string FilePath(string filename = "progress.sav")
    {
        return Path.Combine(Root, filename);
    }

    public string WriteFile(string filename = "progress.sav", string contents = "initial")
    {
        var path = FilePath(filename);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Signal(WatcherChangeTypes kind = WatcherChangeTypes.Changed, string filename = "progress.sav")
    {
        var args = new FileSystemEventArgs(kind, Root, filename);
        ChangedMethod.Invoke(Watcher, new object[] { this, args });
    }

    public void MoveTime(int milliseconds)
    {
        CurrentTime = CurrentTime.AddMilliseconds(milliseconds);
    }

    public void ResetCaptureSignal()
    {
        _captureOccurred = NewSignal();
    }

    public Task WaitForCaptureAsync()
    {
        return _captureOccurred.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        Watcher.Stop().GetAwaiter().GetResult();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private byte[]? Capture(int seconds)
    {
        _captureRequests.Enqueue(seconds);
        if (FailCapture)
        {
            throw new InvalidOperationException("Simulated screenshot failure");
        }

        var result = Screenshot;
        _captureOccurred.TrySetResult(true);
        return result;
    }

    private static TaskCompletionSource<bool> NewSignal()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
