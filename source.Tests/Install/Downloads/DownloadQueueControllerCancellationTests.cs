using Graviton.Install.Downloads;
using Graviton.Tests.Fakes.Downloads;

using Xunit;

using System.IO;
using System.Net;
using System.Net.Http;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueControllerCancellationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Cancel_QueuedRequest_DoesNotDownloadAndCallsCancelledOnce()
    {
        using var harness = new DownloadQueueTestHarness(maxConcurrent: 1);
        var first = harness.CreateRequest("first");
        var second = harness.CreateRequest("second");
        var releaseFirst = new TaskCompletionSource<RawClientResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = 0;
        var canceled = 0;
        var completed = 0;

        second.OnCancelled = () =>
        {
            Interlocked.Increment(ref canceled);
            return Task.CompletedTask;
        };
        second.OnDownloadComplete = (item, download) =>
        {
            Interlocked.Increment(ref completed);
            return Task.CompletedTask;
        };

        harness.ConfigureDownloads(path =>
        {
            if (path == first.DownloadUrl)
            {
                firstStarted.TrySetResult(true);
                return releaseFirst.Task;
            }

            Interlocked.Increment(ref secondStarted);
            return Task.FromResult<RawClientResponse?>(DownloadQueueTestHarness.Response(new byte[] { 2 }));
        });

        try
        {
            harness.Enqueue(first);
            await firstStarted.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            harness.Enqueue(second);
            await harness.WaitForWorkersAsync(2);
            harness.Queue.Cancel(second.Id);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await second.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            });

            releaseFirst.TrySetResult(DownloadQueueTestHarness.Response(new byte[] { 1 }));
            await first.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await harness.WaitForAllWorkersAsync();

            Assert.Equal(0, secondStarted);
            Assert.Equal(1, canceled);
            Assert.Equal(0, completed);
            Assert.False(harness.Queue.IsDownloading(second.Id));
        }
        finally
        {
            releaseFirst.TrySetResult(DownloadQueueTestHarness.Response(Array.Empty<byte>()));
        }
    }

    [Fact]
    public async Task Cancel_WhileWaitingForInstallation_SkipsCompletionCallback()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("game");
        var installation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = 0;
        var completed = 0;

        request.WaitForInstall = installation.Task;
        request.OnCancelled = () =>
        {
            Interlocked.Increment(ref canceled);
            return Task.CompletedTask;
        };
        request.OnDownloadComplete = (item, download) =>
        {
            Interlocked.Increment(ref completed);
            return Task.CompletedTask;
        };

        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, new byte[] { 1 });

        try
        {
            harness.Enqueue(request);
            var queueItem = Assert.Single(harness.ViewModel.Items);
            await harness.WaitUntilAsync(() =>
            {
                return queueItem.Status == DownloadStatus.Waiting;
            });

            harness.Queue.Cancel(request.Id);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            });
            await harness.WaitForAllWorkersAsync();

            Assert.Equal(1, canceled);
            Assert.Equal(0, completed);
            Assert.False(harness.Queue.IsDownloading(request.Id));
        }
        finally
        {
            installation.TrySetResult(true);
        }
    }

    [Fact]
    public async Task Enqueue_WithHttpError_CallsFailureCallbackAndRemovesBackup()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("failed");
        var failures = new List<Exception>();
        var cancellations = 0;

        request.OnFailed = exception =>
        {
            failures.Add(exception);
            return Task.CompletedTask;
        };
        request.OnCancelled = () =>
        {
            Interlocked.Increment(ref cancellations);
            return Task.CompletedTask;
        };

        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, Array.Empty<byte>(), HttpStatusCode.ServiceUnavailable);
        harness.Enqueue(request);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        });
        await harness.WaitForAllWorkersAsync();

        Assert.Single(failures);
        Assert.Equal(0, cancellations);
        Assert.False(harness.Queue.IsDownloading(request.Id));
        Assert.False(File.Exists(request.DownloadPath));
    }

    [Fact]
    public async Task Enqueue_WhenInstallCallbackThrows_CallsFailureInsteadOfCompleting()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("failure-on-install");
        var exception = new InvalidOperationException("Failed during installation");
        Exception? reported = null;
        var canceled = 0;

        request.OnDownloadComplete = (item, download) =>
        {
            throw exception;
        };
        request.OnFailed = failure =>
        {
            reported = failure;
            return Task.CompletedTask;
        };
        request.OnCancelled = () =>
        {
            Interlocked.Increment(ref canceled);
            return Task.CompletedTask;
        };

        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, new byte[] { 1 });
        harness.Enqueue(request);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        });
        await harness.WaitForAllWorkersAsync();

        Assert.Same(exception, actual);
        Assert.Same(exception, reported);
        Assert.Equal(0, canceled);
        Assert.False(harness.Queue.IsDownloading(request.Id));
    }

    [Fact]
    public async Task Cancel_UnknownRequest_DoesNotCancelActiveDownload()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("active");
        var release = new TaskCompletionSource<RawClientResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.ConfigureDownloads(path =>
        {
            started.TrySetResult(true);
            return release.Task;
        });

        try
        {
            harness.Enqueue(request);
            await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            harness.Queue.Cancel("unknown");
            Assert.True(harness.Queue.IsDownloading(request.Id));

            release.TrySetResult(DownloadQueueTestHarness.Response(new byte[] { 1 }));
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await harness.WaitForAllWorkersAsync();

            Assert.True(request.InstallCompletion.Task.IsCompletedSuccessfully);
        }
        finally
        {
            release.TrySetResult(DownloadQueueTestHarness.Response(Array.Empty<byte>()));
        }
    }
    [Fact]
    public async Task Enqueue_WhenWaitForInstallFails_ReportsFailureAndDoesNotComplete()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("failed-install-wait");
        var error = new IOException("Install prerequisite failed");
        Exception? reported = null;
        var completed = 0;

        request.WaitForInstall = Task.FromException(error);
        request.OnFailed = failure =>
        {
            reported = failure;
            return Task.CompletedTask;
        };
        request.OnDownloadComplete = (item, download) =>
        {
            Interlocked.Increment(ref completed);
            return Task.CompletedTask;
        };

        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, new byte[] { 1 });
        harness.Enqueue(request);

        var observed = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        });
        await harness.WaitForAllWorkersAsync();

        Assert.Same(error, observed);
        Assert.Same(error, reported);
        Assert.Equal(0, completed);
        Assert.False(harness.Queue.IsDownloading(request.Id));
    }

    [Fact]
    public async Task Enqueue_AfterFailedDownload_AllowsNewRequestWithSameId()
    {
        using var harness = new DownloadQueueTestHarness();
        var first = harness.CreateRequest("retry");
        harness.RomM.ConfigureRawGETResponse(first.DownloadUrl, Array.Empty<byte>(), HttpStatusCode.ServiceUnavailable);

        harness.Enqueue(first);
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await first.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        });
        await harness.WaitForAllWorkersAsync();

        var retry = harness.CreateRequest("retry");
        var completed = 0;
        retry.OnDownloadComplete = (item, download) =>
        {
            Interlocked.Increment(ref completed);
            return Task.CompletedTask;
        };
        harness.RomM.ConfigureRawGETResponse(retry.DownloadUrl, new byte[] { 1, 2, 3 });

        harness.Enqueue(retry);
        await retry.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await harness.WaitForAllWorkersAsync();

        Assert.Equal(1, completed);
        Assert.False(harness.Queue.IsDownloading(retry.Id));
    }

    [Fact]
    public async Task Enqueue_WhenRomMReturnsNull_ReportsFailureAndDoesNotComplete()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("null-response");
        Exception? reported = null;
        var completed = 0;

        request.OnFailed = failure =>
        {
            reported = failure;
            return Task.CompletedTask;
        };
        request.OnDownloadComplete = (item, download) =>
        {
            Interlocked.Increment(ref completed);
            return Task.CompletedTask;
        };
        harness.ConfigureDownloads(path =>
        {
            return Task.FromResult<RawClientResponse?>(null);
        });

        harness.Enqueue(request);
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        });
        await harness.WaitForAllWorkersAsync();

        Assert.NotNull(reported);
        Assert.Equal(0, completed);
        Assert.False(harness.Queue.IsDownloading(request.Id));
    }

}
