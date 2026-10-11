using Graviton.Install.Downloads;

using Xunit;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueItemTests
{
    [Fact]
    public void SetStatus_UpdatesStatusAndText()
    {
        var item = new DownloadQueueItem();

        item.SetStatus(DownloadStatus.Downloading, "Downloading game");

        Assert.Equal(DownloadStatus.Downloading, item.Status);
        Assert.Equal("Downloading game", item.StatusText);
    }

    [Fact]
    public void SetStatus_NotifiesStatusAndTextChanges()
    {
        var item = new DownloadQueueItem();
        var changedProperties = new List<string?>();

        item.PropertyChanged += (sender, args) =>
        {
            changedProperties.Add(args.PropertyName);
        };

        item.SetStatus(DownloadStatus.Completed, "Download completed");

        Assert.Collection(
            changedProperties,
            property =>
            {
                Assert.Equal(nameof(DownloadQueueItem.Status), property);
            },
            property =>
            {
                Assert.Equal(nameof(DownloadQueueItem.StatusText), property);
            });
    }

    [Fact]
    public void SetProgress_WithKnownTotal_UpdatesProgress()
    {
        var item = new DownloadQueueItem();

        item.SetProgress(256, 1024, false);

        Assert.Equal(256, item.ProgressValue);
        Assert.Equal(1024, item.ProgressMaximum);
        Assert.False(item.IsIndeterminate);
    }

    [Fact]
    public void SetProgress_WithUnknownTotal_UsesIndeterminateProgress()
    {
        var item = new DownloadQueueItem();

        item.SetProgress(256, 256, true);

        Assert.Equal(256, item.ProgressValue);
        Assert.Equal(256, item.ProgressMaximum);
        Assert.True(item.IsIndeterminate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetProgress_WithNonPositiveMaximum_UsesOne(double maximum)
    {
        var item = new DownloadQueueItem();

        item.SetProgress(0, maximum, false);

        Assert.Equal(1, item.ProgressMaximum);
    }

    [Fact]
    public void SetProgress_NotifiesAllProgressChanges()
    {
        var item = new DownloadQueueItem();
        var changedProperties = new List<string?>();

        item.PropertyChanged += (sender, args) =>
        {
            changedProperties.Add(args.PropertyName);
        };

        item.SetProgress(50, 100, false);

        Assert.Collection(
            changedProperties,
            property =>
            {
                Assert.Equal(nameof(DownloadQueueItem.ProgressValue), property);
            },
            property =>
            {
                Assert.Equal(nameof(DownloadQueueItem.ProgressMaximum), property);
            },
            property =>
            {
                Assert.Equal(nameof(DownloadQueueItem.IsIndeterminate), property);
            });
    }
}
