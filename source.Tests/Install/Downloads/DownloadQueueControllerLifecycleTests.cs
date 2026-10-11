using Graviton.Install.Downloads;
using Graviton.Tests.Fakes.Downloads;

using System.IO;
using System.Net;
using System.Net.Http;

using Xunit;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueControllerLifecycleTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Enqueue_WhileRequestIsActive_DisplaysMatchingQueueItem()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("visible");
        request.DisplayName = "Example download";
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<RawClientResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.ConfigureDownloads(path =>
        {
            started.TrySetResult(true);
            return release.Task;
        });

        try
        {
            harness.Enqueue(request);
            await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            var item = Assert.Single(harness.ViewModel.Items);
            Assert.Equal(request.Id, item.Id);
            Assert.Equal(request.DisplayName, item.DisplayName);
            Assert.Equal(DownloadStatus.Downloading, item.Status);
            Assert.True(harness.Queue.IsDownloading(request.Id));

            release.TrySetResult(DownloadQueueTestHarness.Response(new byte[] { 1 }));
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await harness.WaitForAllWorkersAsync();

            Assert.Empty(harness.ViewModel.Items);
        }
        finally
        {
            release.TrySetResult(DownloadQueueTestHarness.Response(Array.Empty<byte>()));
        }
    }

    [Fact]
    public async Task Enqueue_OnSuccessfulInstallation_UpdatesStatusAndRemovesItem()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("success");
        DownloadQueueItem? itemSeenByCallback = null;
        var wasActiveDuringCallback = false;

        request.OnDownloadComplete = (item, download) =>
        {
            itemSeenByCallback = item;
            wasActiveDuringCallback = harness.Queue.IsDownloading(download.Id);
            Assert.Contains(item, harness.ViewModel.Items);
            return Task.CompletedTask;
        };
        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, new byte[] { 4, 5, 6 });

        harness.Enqueue(request);
        await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await harness.WaitForAllWorkersAsync();

        Assert.NotNull(itemSeenByCallback);
        Assert.True(wasActiveDuringCallback);
        Assert.Equal(DownloadStatus.Completed, itemSeenByCallback.Status);
        Assert.Equal(itemSeenByCallback.ProgressMaximum, itemSeenByCallback.ProgressValue);
        Assert.Empty(harness.ViewModel.Items);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Enqueue_WhenServerReturnsError_SetsFailedStatusAndRemovesItem(HttpStatusCode status)
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest($"error-{(int)status}");
        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, Array.Empty<byte>(), status);

        harness.Enqueue(request);
        var item = Assert.Single(harness.ViewModel.Items);
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        });
        await harness.WaitForAllWorkersAsync();

        Assert.Equal(DownloadStatus.Failed, item.Status);
        Assert.Empty(harness.ViewModel.Items);
        Assert.False(harness.Queue.IsDownloading(request.Id));
    }

    [Fact]
    public async Task Cancel_AfterDownloadCompletes_DoesNotInvokeCancellationCallback()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("finished");
        var canceled = 0;
        request.OnCancelled = () =>
        {
            Interlocked.Increment(ref canceled);
            return Task.CompletedTask;
        };
        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, new byte[] { 1 });

        harness.Enqueue(request);
        await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await harness.WaitForAllWorkersAsync();

        harness.Queue.Cancel(request.Id);

        Assert.Equal(0, canceled);
        Assert.True(request.InstallCompletion.Task.IsCompletedSuccessfully);
        Assert.False(harness.Queue.IsDownloading(request.Id));
    }

    [Fact]
    public async Task Cancel_WhileServerIsResponding_CancelsWhenResponseArrives()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("slow-response");
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<RawClientResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = 0;
        var completed = 0;

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
        harness.ConfigureDownloads(path =>
        {
            started.TrySetResult(true);
            return release.Task;
        });

        try
        {
            harness.Enqueue(request);
            await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            harness.Queue.Cancel(request.Id);
            Assert.False(request.InstallCompletion.Task.IsCompleted);

            release.TrySetResult(DownloadQueueTestHarness.Response(new byte[] { 1, 2, 3 }));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            });
            await harness.WaitForAllWorkersAsync();

            Assert.Equal(1, canceled);
            Assert.Equal(0, completed);
            Assert.False(harness.Queue.IsDownloading(request.Id));
            Assert.False(File.Exists(request.DownloadPath));
        }
        finally
        {
            release.TrySetResult(DownloadQueueTestHarness.Response(Array.Empty<byte>()));
        }
    }
}
