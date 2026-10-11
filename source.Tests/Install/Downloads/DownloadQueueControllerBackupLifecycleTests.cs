using Graviton.Install.Downloads;
using Graviton.Models.Install;
using Graviton.Tests.Fakes.Downloads;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueControllerBackupLifecycleTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Enqueue_WhileDownloadIsActive_WritesRecoverableBackup()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("in-progress");
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

            var backupPath = GetBackupPath(harness, request);
            Assert.True(File.Exists(backupPath));

            var backup = JsonSerializer.Deserialize<DownloadRequestBackup>(File.ReadAllText(backupPath));
            Assert.NotNull(backup);
            Assert.Equal(request.Id, backup.ID);
            Assert.Equal(request.Id, backup.GameID);
            Assert.Equal(request.DownloadPath, backup.DownloadPath);
            Assert.Equal(InstallType.BaseGame, backup.InstallType);

            release.TrySetResult(DownloadQueueTestHarness.Response(new byte[] { 1 }));
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await harness.WaitForAllWorkersAsync();
        }
        finally
        {
            release.TrySetResult(DownloadQueueTestHarness.Response(Array.Empty<byte>()));
        }
    }

    [Fact]
    public async Task Enqueue_WhenDownloadCompletes_RemovesRecoveryBackup()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("completed");
        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, new byte[] { 1, 2, 3 });

        harness.Enqueue(request);
        await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await harness.WaitForAllWorkersAsync();

        Assert.False(File.Exists(GetBackupPath(harness, request)));
        Assert.False(harness.Queue.IsDownloading(request.Id));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Enqueue_WhenServerRejectsDownload_RemovesRecoveryBackup(HttpStatusCode status)
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest($"failed-{(int)status}");
        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, Array.Empty<byte>(), status);

        harness.Enqueue(request);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        });
        await harness.WaitForAllWorkersAsync();

        Assert.False(File.Exists(GetBackupPath(harness, request)));
        Assert.False(harness.Queue.IsDownloading(request.Id));
    }

    [Fact]
    public async Task Enqueue_WhileWaitingForInstallation_RetainsBackupUntilFinished()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("waiting");
        var finishInstallation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        request.WaitForInstall = finishInstallation.Task;
        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, new byte[] { 1 });

        try
        {
            harness.Enqueue(request);
            var item = Assert.Single(harness.ViewModel.Items);
            await harness.WaitUntilAsync(() =>
            {
                return item.Status == DownloadStatus.Waiting;
            });

            Assert.True(File.Exists(GetBackupPath(harness, request)));
            Assert.True(harness.Queue.IsDownloading(request.Id));

            finishInstallation.TrySetResult(true);
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await harness.WaitForAllWorkersAsync();

            Assert.False(File.Exists(GetBackupPath(harness, request)));
        }
        finally
        {
            finishInstallation.TrySetResult(true);
        }
    }

    [Fact]
    public async Task Cancel_QueuedDownload_RemovesOnlyItsRecoveryBackup()
    {
        using var harness = new DownloadQueueTestHarness(maxConcurrent: 1);
        var active = harness.CreateRequest("active");
        var queued = harness.CreateRequest("queued");
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<RawClientResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.ConfigureDownloads(path =>
        {
            if (path == active.DownloadUrl)
            {
                started.TrySetResult(true);
                return release.Task;
            }

            return Task.FromResult<RawClientResponse?>(DownloadQueueTestHarness.Response(new byte[] { 2 }));
        });

        try
        {
            harness.Enqueue(active);
            await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            harness.Enqueue(queued);
            await harness.WaitForWorkersAsync(2);

            Assert.True(File.Exists(GetBackupPath(harness, active)));
            Assert.True(File.Exists(GetBackupPath(harness, queued)));

            harness.Queue.Cancel(queued.Id);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await queued.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            });

            await harness.WaitUntilAsync(() =>
            {
                return !File.Exists(GetBackupPath(harness, queued));
            });
            Assert.False(harness.Queue.IsDownloading(queued.Id));
            Assert.True(File.Exists(GetBackupPath(harness, active)));

            release.TrySetResult(DownloadQueueTestHarness.Response(new byte[] { 1 }));
            await active.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await harness.WaitForAllWorkersAsync();
        }
        finally
        {
            release.TrySetResult(DownloadQueueTestHarness.Response(Array.Empty<byte>()));
        }
    }

    [Fact]
    public async Task Enqueue_WhenCompletionCallbackThrows_RemovesRecoveryBackup()
    {
        using var harness = new DownloadQueueTestHarness();
        var request = harness.CreateRequest("install-failed");
        var failure = new IOException("Installation failed");

        request.OnDownloadComplete = (item, download) =>
        {
            throw failure;
        };
        harness.RomM.ConfigureRawGETResponse(request.DownloadUrl, new byte[] { 1 });

        harness.Enqueue(request);
        var actual = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await request.InstallCompletion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        });
        await harness.WaitForAllWorkersAsync();

        Assert.Same(failure, actual);
        Assert.False(File.Exists(GetBackupPath(harness, request)));
        Assert.False(harness.Queue.IsDownloading(request.Id));
    }

    private static string GetBackupPath(DownloadQueueTestHarness harness, DownloadRequest request)
    {
        return Path.Combine(harness.Plugin.Context.PluginDataPath, "temp", "downloads", $"{request.Id}.tmp");
    }
}
