using Graviton.Install.Downloads;

using System.IO;

using Xunit;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueControllerCopyReliabilityTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "GravitonTests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(524287, 0)]
    [InlineData(524288, 1)]
    [InlineData(524289, 1)]
    [InlineData(1048575, 1)]
    [InlineData(1048576, 2)]
    [InlineData(1048577, 2)]
    public async Task CopyDownloadToFileAsync_ReportsProgressAtExpectedThresholds(int size, int expectedCount)
    {
        var data = CreateData(size);
        var path = Path.Combine(_temporaryDirectory, $"{size}.bin");
        var reports = new List<long>();

        using var source = new MemoryStream(data);

        await DownloadQueueController.CopyDownloadToFileAsync(
            source: source,
            downloadPath: path,
            onProgress: downloaded =>
            {
                reports.Add(downloaded);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expectedCount, reports.Count);
        Assert.Equal(data, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));

        for (var index = 0; index < reports.Count; index++)
        {
            var previous = index == 0 ? 0 : reports[index - 1];

            Assert.True(reports[index] > previous);
            Assert.True(reports[index] <= size);
        }
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_ConcurrentTransfers_PreserveIndependentFiles()
    {
        var firstData = CreateData(1024 * 1024 + 11);
        var secondData = CreateData(1024 * 1024 + 97).Reverse().ToArray();
        var firstPath = Path.Combine(_temporaryDirectory, "first", "download.bin");
        var secondPath = Path.Combine(_temporaryDirectory, "second", "download.bin");

        using var firstSource = new MemoryStream(firstData);
        using var secondSource = new MemoryStream(secondData);

        var first = DownloadQueueController.CopyDownloadToFileAsync(
            source: firstSource,
            downloadPath: firstPath,
            onProgress: downloaded =>
            {
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var second = DownloadQueueController.CopyDownloadToFileAsync(
            source: secondSource,
            downloadPath: secondPath,
            onProgress: downloaded =>
            {
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await Task.WhenAll(first, second);

        Assert.Equal(firstData, await File.ReadAllBytesAsync(firstPath, TestContext.Current.CancellationToken));
        Assert.Equal(secondData, await File.ReadAllBytesAsync(secondPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_CancellingOneTransfer_DoesNotCancelAnother()
    {
        var cancelledData = CreateData(1024 * 1024);
        var completedData = CreateData(1024 * 1024 + 29);
        var cancelledPath = Path.Combine(_temporaryDirectory, "cancelled.bin");
        var completedPath = Path.Combine(_temporaryDirectory, "completed.bin");

        using var cancellation = new CancellationTokenSource();
        using var cancelledSource = new MemoryStream(cancelledData);
        using var completedSource = new MemoryStream(completedData);

        var cancelled = Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await DownloadQueueController.CopyDownloadToFileAsync(
                source: cancelledSource,
                downloadPath: cancelledPath,
                onProgress: downloaded =>
                {
                    cancellation.Cancel();
                },
                cancellationToken: cancellation.Token);
        });

        var completed = DownloadQueueController.CopyDownloadToFileAsync(
            source: completedSource,
            downloadPath: completedPath,
            onProgress: downloaded =>
            {
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await Task.WhenAll(cancelled, completed);

        var partial = await File.ReadAllBytesAsync(cancelledPath, TestContext.Current.CancellationToken);

        Assert.Equal(cancelledData.Take(524288).ToArray(), partial);
        Assert.Equal(completedData, await File.ReadAllBytesAsync(completedPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_WhenSourceThrows_LeavesPartialFileAndPreservesUnrelatedFile()
    {
        var path = Path.Combine(_temporaryDirectory, "download.bin");
        var unrelatedPath = Path.Combine(_temporaryDirectory, "existing.sav");
        var original = new byte[] { 0x00, 0xFF, 0x42, 0x80 };

        Directory.CreateDirectory(_temporaryDirectory);
        await File.WriteAllBytesAsync(unrelatedPath, original, TestContext.Current.CancellationToken);

        using var source = new FailingReadStream(CreateData(4096));

        await Assert.ThrowsAsync<IOException>(async () =>
        {
            await DownloadQueueController.CopyDownloadToFileAsync(
                source: source,
                downloadPath: path,
                onProgress: downloaded =>
                {
                },
                cancellationToken: TestContext.Current.CancellationToken);
        });

        Assert.Equal(128, new FileInfo(path).Length);
        Assert.Equal(original, await File.ReadAllBytesAsync(unrelatedPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_AfterFailedRead_RetryReplacesPartialFile()
    {
        var path = Path.Combine(_temporaryDirectory, "download.bin");
        var data = CreateData(2048);

        using (var failingSource = new FailingReadStream(data))
        {
            await Assert.ThrowsAsync<IOException>(async () =>
            {
                await DownloadQueueController.CopyDownloadToFileAsync(
                    source: failingSource,
                    downloadPath: path,
                    onProgress: downloaded =>
                    {
                    },
                    cancellationToken: TestContext.Current.CancellationToken);
            });
        }

        using var retrySource = new MemoryStream(data);

        await DownloadQueueController.CopyDownloadToFileAsync(
            source: retrySource,
            downloadPath: path,
            onProgress: downloaded =>
            {
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(data, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_WithShortReads_PreservesFullDataAndMonotonicProgress()
    {
        var data = CreateData(1024 * 1024 + 20000);
        var path = Path.Combine(_temporaryDirectory, "short-reads.bin");
        var reports = new List<long>();

        using var source = new ShortReadStream(data);

        await DownloadQueueController.CopyDownloadToFileAsync(
            source: source,
            downloadPath: path,
            onProgress: downloaded =>
            {
                reports.Add(downloaded);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(data, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(2, reports.Count);
        Assert.True(reports[0] >= 524288);
        Assert.True(reports[1] >= 1048576);
        Assert.True(reports[1] > reports[0]);
        Assert.All(reports, value =>
        {
            Assert.True(value <= data.Length);
        });
    }

    [Fact]
    public async Task CopyDownloadToFileAsync_WhenProgressCallbackThrows_StopsCopying()
    {
        var data = CreateData(1024 * 1024);
        var path = Path.Combine(_temporaryDirectory, "callback.bin");

        using var source = new MemoryStream(data);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await DownloadQueueController.CopyDownloadToFileAsync(
                source: source,
                downloadPath: path,
                onProgress: downloaded =>
                {
                    throw new InvalidOperationException("Progress observer failed.");
                },
                cancellationToken: TestContext.Current.CancellationToken);
        });

        Assert.Equal(data.Take(524288).ToArray(), await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    private static byte[] CreateData(int length)
    {
        var data = new byte[length];

        for (var index = 0; index < data.Length; index++)
        {
            data[index] = (byte)(index % 251);
        }

        return data;
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private sealed class FailingReadStream : MemoryStream
    {
        private bool _hasRead;

        public FailingReadStream(byte[] data) : base(data)
        {
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_hasRead)
            {
                throw new IOException("The source stream failed.");
            }

            _hasRead = true;
            return base.ReadAsync(buffer, offset, Math.Min(count, 128), cancellationToken);
        }
    }

    private sealed class ShortReadStream : MemoryStream
    {
        public ShortReadStream(byte[] data) : base(data)
        {
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return base.ReadAsync(buffer, offset, Math.Min(count, 7000), cancellationToken);
        }
    }
}
