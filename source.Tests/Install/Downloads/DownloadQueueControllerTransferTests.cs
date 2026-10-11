using Graviton.Install.Downloads;

using Xunit;

using System.IO;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueControllerTransferTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "GravitonTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CopyDownloadToFileAsync_PreservesBinaryDataAndCreatesDirectory()
    {
        var expected = new byte[] { 0x00, 0xFF, 0x42, 0x80, 0x10 };
        var path = Path.Combine(_temporaryDirectory, "nested", "download.bin");

        var progressUpdates = new List<long>();

        using var source = new MemoryStream(expected);

        await DownloadQueueController.CopyDownloadToFileAsync(
            source: source,
            downloadPath: path,
            onProgress: downloaded =>
            {
                progressUpdates.Add(downloaded);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var actual = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
        Assert.Empty(progressUpdates);
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_WithLargeFile_ReportsProgress()
    {
        var expected = Enumerable.Range(0, 1024 * 1280)
            .Select(index => (byte)(index % 251))
            .ToArray();
        var path = Path.Combine(_temporaryDirectory, "download.bin");
        var progressUpdates = new List<long>();

        using var source = new MemoryStream(expected);

        await DownloadQueueController.CopyDownloadToFileAsync(
            source: source,
            downloadPath: path,
            onProgress: downloaded =>
            {
                progressUpdates.Add(downloaded);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new long[] { 524288L, 1048576L }, progressUpdates);
        Assert.Equal(expected, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_WhenCancelled_StopsBeforeCopying()
    {
        var path = Path.Combine(_temporaryDirectory, "download.bin");
        var expected = new byte[] { 1, 2, 3, 4 };

        using var source = new MemoryStream(expected);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await DownloadQueueController.CopyDownloadToFileAsync(
                source: source,
                downloadPath: path,
                onProgress: downloaded =>
                {
                },
                cancellationToken: cancellation.Token);
        });

        Assert.Empty(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
