using Graviton.Install.Downloads;
using Graviton.Models.Install;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;
using NSubstitute.Extensions;

using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Graviton.Tests.Fakes.Downloads;

internal sealed class DownloadQueueTestHarness : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "GravitonTests", Guid.NewGuid().ToString("N"));
    private readonly List<TaskCompletionSource<bool>> _workerStarts = new();
    private readonly List<Task> _workerTasks = new();
    private readonly object _workerLock = new();

    public GravitonContextFake Plugin { get; } = new();
    public PlayniteFake Playnite { get; } = new();
    public RomMServerFake RomM { get; } = new();
    public DownloadQueueViewModel ViewModel { get; } = new();
    public DownloadQueueController Queue { get; }

    public DownloadQueueTestHarness(int maxConcurrent = 1)
    {
        Directory.CreateDirectory(_directory);
        Plugin.ConfigurePluginDataDirectory(_directory);

        Queue = new DownloadQueueController(
            plugin: Plugin.Context,
            playniteAPI: Playnite.Api,
            logger: new GravitonLogger(Plugin.Settings),
            romMServer: RomM.Server,
            downloadQueueVM: ViewModel,
            maxConcurrent: maxConcurrent,
            dispatchToUI: action =>
            {
                action();
            },
            finishDelay: () =>
            {
                return Task.CompletedTask;
            },
            startWorker: work =>
            {
                StartWorker(work);
            });
    }

    public DownloadRequest CreateRequest(string id)
    {
        return new DownloadRequest
        {
            Id = id,
            DisplayName = id,
            DownloadUrl = $"/test/downloads/{id}",
            DownloadPath = Path.Combine(_directory, "temp", id, "download.bin")
        };
    }

    public void Enqueue(DownloadRequest request)
    {
        Queue.Enqueue(request, new DownloadRequestBackup
        {
            ID = request.Id,
            GameID = request.Id,
            DownloadPath = request.DownloadPath,
            InstallType = InstallType.BaseGame
        });
    }

    public void ConfigureDownloads(Func<string, Task<RawClientResponse?>> responder)
    {
        RomM.Server.Configure().RawGETAsync(
            APIPath: Arg.Any<string>(),
            PublicEndpoint: Arg.Any<bool>())
            .Returns(call =>
            {
                var path = call.ArgAt<string>(0);
                return responder(path);
            });
    }

    public static RawClientResponse Response(byte[] bytes, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new RawClientResponse(new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(bytes)
        });
    }

    public async Task WaitForWorkersAsync(int count)
    {
        Task[] starts;

        lock (_workerLock)
        {
            if (_workerStarts.Count < count)
            {
                throw new InvalidOperationException($"Expected {count} queued workers, found {_workerStarts.Count}.");
            }

            starts = _workerStarts.Take(count).Select(source => source.Task).ToArray();
        }

        await Task.WhenAll(starts).WaitAsync(Timeout);
    }

    public async Task WaitUntilAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();

        while (!condition())
        {
            if (watch.Elapsed >= Timeout)
            {
                throw new TimeoutException("The download queue did not reach the expected state.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    public async Task WaitForAllWorkersAsync()
    {
        Task[] workers;

        lock (_workerLock)
        {
            workers = _workerTasks.ToArray();
        }

        await Task.WhenAll(workers).WaitAsync(Timeout);
    }

    public async Task WaitUntilInactiveAsync(string id)
    {
        await WaitUntilAsync(() =>
        {
            return !Queue.IsDownloading(id);
        });
    }

    private void StartWorker(Func<Task> work)
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_workerLock)
        {
            _workerStarts.Add(started);
        }

        var worker = Task.Run(async () =>
        {
            Task execution;

            try
            {
                execution = work();
            }
            finally
            {
                started.TrySetResult(true);
            }

            await execution;
        });

        lock (_workerLock)
        {
            _workerTasks.Add(worker);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
