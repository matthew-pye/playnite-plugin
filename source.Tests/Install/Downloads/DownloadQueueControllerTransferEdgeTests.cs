using Graviton.Install.Downloads;

using System.IO;

using Xunit;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueControllerTransferEdgeTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "GravitonTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CopyDownloadToFileAsync_WithEmptySource_CreatesEmptyFile()
    {
        var path = Path.Combine(_temporaryDirectory, "empty.bin");
        var progressUpdates = new List<long>();

        using var source = new MemoryStream();

        await DownloadQueueController.CopyDownloadToFileAsync(
            source: source,
            downloadPath: path,
            onProgress: downloaded =>
            {
                progressUpdates.Add(downloaded);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(path));
        Assert.Empty(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(progressUpdates);
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_WhenFileExists_ReplacesAndTruncatesIt()
    {
        var path = Path.Combine(_temporaryDirectory, "download.bin");
        var original = Enumerable.Repeat((byte)0xFF, 4096).ToArray();
        var replacement = new byte[] { 0x00, 0x42, 0x80 };

        Directory.CreateDirectory(_temporaryDirectory);
        await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);

        using var source = new MemoryStream(replacement);

        await DownloadQueueController.CopyDownloadToFileAsync(
            source: source,
            downloadPath: path,
            onProgress: downloaded =>
            {
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var actual = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(replacement, actual);
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_WhenCancelledAfterProgress_StopsWithPartialFile()
    {
        var path = Path.Combine(_temporaryDirectory, "download.bin");
        var data = Enumerable.Range(0, 3 * 256 * 1024)
            .Select(index => (byte)(index % 251))
            .ToArray();
        var progressUpdates = new List<long>();

        using var source = new MemoryStream(data);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await DownloadQueueController.CopyDownloadToFileAsync(
                source: source,
                downloadPath: path,
                onProgress: downloaded =>
                {
                    progressUpdates.Add(downloaded);
                    cancellation.Cancel();
                },
                cancellationToken: cancellation.Token);
        });

        var partial = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        var expectedPartial = data.Take(512 * 1024).ToArray();

        Assert.Equal(new long[] { 512 * 1024L }, progressUpdates);
        Assert.Equal(expectedPartial, partial);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
