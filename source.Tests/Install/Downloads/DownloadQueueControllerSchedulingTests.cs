using Graviton.Install.Downloads;
using Graviton.Tests.Fakes.Downloads;

using Xunit;

using System.IO;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueControllerSchedulingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Enqueue_WithOneSlot_DoesNotStartSecondDownloadUntilFirstReleasesSlot()
    {
        using var harness = new DownloadQueueTestHarness(maxConcurrent: 1);
        var first = harness.CreateRequest("first");
        var second = harness.CreateRequest("second");
        var releaseFirst = new TaskCompletionSource<RawClientResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.ConfigureDownloads(path =>
        {
            if (path == first.DownloadUrl)
            {
                firstStarted.TrySetResult(true);
                return releaseFirst.Task;
            }

            secondStarted.TrySetResult(true);
            return Task.FromResult<RawClientResponse?>(DownloadQueueTestHarness.Response(new byte[] { 2 }));
        });

        try
        {
            harness.Enqueue(first);
            await firstStarted.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            harness.Enqueue(second);
            await harness.WaitForWorkersAsync(2);

            Assert.True(harness.Queue.IsDownloading(first.Id));
            Assert.True(harness.Queue.IsDownloading(second.Id));
            Assert.False(secondStarted.Task.IsCompleted);

            releaseFirst.TrySetResult(DownloadQueueTestHarness.Response(new byte[] { 1 }));
            await Task.WhenAll(first.InstallCompletion.Task, second.InstallCompletion.Task).WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await secondStarted.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            await harness.WaitUntilInactiveAsync(first.Id);
            await harness.WaitUntilInactiveAsync(second.Id);
            await harness.WaitForAllWorkersAsync();
        }
        finally
        {
            releaseFirst.TrySetResult(DownloadQueueTestHarness.Response(Array.Empty<byte>()));
        }
    }

    [Fact]
    public async Task Enqueue_WithTwoSlots_StartsTwoDownloadsButQueuesThird()
    {
        using var harness = new DownloadQueueTestHarness(maxConcurrent: 2);
        var first = harness.CreateRequest("first");
        var second = harness.CreateRequest("second");
        var third = harness.CreateRequest("third");
        var releaseFirst = new TaskCompletionSource<RawClientResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource<RawClientResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.ConfigureDownloads(path =>
        {
            if (path == first.DownloadUrl)
            {
                firstStarted.TrySetResult(true);
                return releaseFirst.Task;
            }

            if (path == second.DownloadUrl)
            {
                secondStarted.TrySetResult(true);
                return releaseSecond.Task;
            }

            thirdStarted.TrySetResult(true);
            return Task.FromResult<RawClientResponse?>(DownloadQueueTestHarness.Response(new byte[] { 3 }));
        });

        try
        {
            harness.Enqueue(first);
            harness.Enqueue(second);
            await Task.WhenAll(firstStarted.Task, secondStarted.Task).WaitAsync(Timeout, TestContext.Current.CancellationToken);

            harness.Enqueue(third);
            await harness.WaitForWorkersAsync(3);
            Assert.False(thirdStarted.Task.IsCompleted);

            releaseFirst.TrySetResult(DownloadQueueTestHarness.Response(new byte[] { 1 }));
            await thirdStarted.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.False(second.InstallCompletion.Task.IsCompleted);

            releaseSecond.TrySetResult(DownloadQueueTestHarness.Response(new byte[] { 2 }));
            await Task.WhenAll(first.InstallCompletion.Task, second.InstallCompletion.Task, third.InstallCompletion.Task).WaitAsync(Timeout, TestContext.Current.CancellationToken);

            await harness.WaitUntilInactiveAsync(first.Id);
            await harness.WaitUntilInactiveAsync(second.Id);
            await harness.WaitForAllWorkersAsync();
            await harness.WaitUntilInactiveAsync(third.Id);
            await harness.WaitForAllWorkersAsync();
        }
        finally
        {
            releaseFirst.TrySetResult(DownloadQueueTestHarness.Response(Array.Empty<byte>()));
            releaseSecond.TrySetResult(DownloadQueueTestHarness.Response(Array.Empty<byte>()));
        }
    }

    [Fact]
    public async Task Enqueue_WaitingForInstallation_DoesNotKeepDownloadSlotOccupied()
    {
        using var harness = new DownloadQueueTestHarness(maxConcurrent: 1);
        var first = harness.CreateRequest("first");
        var second = harness.CreateRequest("second");
        var allowInstallation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.WaitForInstall = allowInstallation.Task;

        harness.RomM.ConfigureRawGETResponse(first.DownloadUrl, new byte[] { 1 });
        harness.RomM.ConfigureRawGETResponse(second.DownloadUrl, new byte[] { 2 });

        try
        {
            harness.Enqueue(first);
            var firstItem = Assert.Single(harness.ViewModel.Items);
            await harness.WaitUntilAsync(() =>
            {
                return firstItem.Status == DownloadStatus.Waiting;
            });

            harness.Enqueue(second);
            await second.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.False(first.InstallCompletion.Task.IsCompleted);
            allowInstallation.TrySetResult(true);
            await first.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            await harness.WaitUntilInactiveAsync(first.Id);
            await harness.WaitUntilInactiveAsync(second.Id);
            await harness.WaitForAllWorkersAsync();
        }
        finally
        {
            allowInstallation.TrySetResult(true);
        }
    }

    [Fact]
    public async Task Enqueue_CompletedDownload_InvokesCompletionCallbackWithDownloadedBytes()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("game");
        var expected = new byte[] { 0, 255, 17, 128 };
        var completions = 0;
        byte[]? observed = null;

        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, expected);
        request.OnDownloadComplete = (item, download) =>
        {
            Interlocked.Increment(ref completions);
            Assert.Equal(request.Id, item.Id);
            Assert.Same(request, download);
            observed = File.ReadAllBytes(download.DownloadPath);
            return Task.CompletedTask;
        };

        harness.Enqueue(request);
        await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await harness.WaitUntilInactiveAsync(request.Id);
        await harness.WaitForAllWorkersAsync();

        Assert.Equal(1, completions);
        Assert.Equal(expected, observed);
        Assert.False(File.Exists(request.DownloadPath));
    }
}
