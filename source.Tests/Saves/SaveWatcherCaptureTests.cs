using Graviton.Tests.Fakes.Saves;

using System.IO;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveWatcherCaptureTests
{
    [Fact]
    public void OnChanged_FirstChangeCapturesImmediately()
    {
        using var test = new SaveWatcherTestHarness();

        test.Signal();

        Assert.Equal(1, test.CaptureCount);
        Assert.Equal(test.Screenshot, test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public void OnChanged_MultipleEventsAtSameTime_CaptureOnlyOnce()
    {
        using var test = new SaveWatcherTestHarness();

        test.Signal();
        test.Signal(WatcherChangeTypes.Created);
        test.Signal(WatcherChangeTypes.Renamed);

        Assert.Equal(1, test.CaptureCount);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(499)]
    [InlineData(500)]
    public void OnChanged_WithinDebounceInterval_DoesNotCaptureAgain(int elapsedMilliseconds)
    {
        using var test = new SaveWatcherTestHarness();
        test.Signal();
        test.MoveTime(elapsedMilliseconds);

        test.Signal();

        Assert.Equal(1, test.CaptureCount);
    }

    [Theory]
    [InlineData(501)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void OnChanged_AfterDebounceInterval_CapturesAgain(int elapsedMilliseconds)
    {
        using var test = new SaveWatcherTestHarness();
        test.Signal();
        test.MoveTime(elapsedMilliseconds);

        test.Signal();

        Assert.Equal(2, test.CaptureCount);
    }

    [Fact]
    public void OnChanged_UsesConfiguredLookbackTime()
    {
        using var test = new SaveWatcherTestHarness();
        test.SecondsBeforeSave = 7;

        test.Signal();

        Assert.Equal(7, Assert.Single(test.CaptureRequests));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    public void OnChanged_UsesLookbackSettingAtTimeOfCapture(int lookback)
    {
        using var test = new SaveWatcherTestHarness();
        test.SecondsBeforeSave = lookback;
        test.Signal();

        Assert.Equal(lookback, Assert.Single(test.CaptureRequests));
    }

    [Fact]
    public void OnChanged_UpdatesScreenshotWhenNewImageIsAvailable()
    {
        using var test = new SaveWatcherTestHarness();
        var first = new byte[] { 1, 2 };
        var second = new byte[] { 3, 4 };
        test.Screenshot = first;
        test.Signal();
        test.MoveTime(501);
        test.Screenshot = second;

        test.Signal();

        Assert.Same(second, test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public void OnChanged_WhenNoImageIsAvailable_PreservesLastScreenshot()
    {
        using var test = new SaveWatcherTestHarness();
        var first = new byte[] { 1, 2 };
        test.Screenshot = first;
        test.Signal();
        test.MoveTime(501);
        test.Screenshot = null;

        test.Signal();

        Assert.Same(first, test.Watcher.NewestSaveScreenshot);
        Assert.Equal(2, test.CaptureCount);
    }

    [Fact]
    public void OnChanged_InitialNullCapture_DoesNotBlockRetry()
    {
        using var test = new SaveWatcherTestHarness();
        test.Screenshot = null;
        test.Signal();
        test.Screenshot = new byte[] { 6 };

        test.Signal();

        Assert.Equal(2, test.CaptureCount);
        Assert.Equal(new byte[] { 6 }, test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public void OnChanged_FailedCapture_DoesNotDiscardPreviousImage()
    {
        using var test = new SaveWatcherTestHarness();
        var original = new byte[] { 8 };
        test.Screenshot = original;
        test.Signal();
        test.MoveTime(501);
        test.FailCapture = true;

        test.Signal();

        Assert.Same(original, test.Watcher.NewestSaveScreenshot);
        Assert.Equal(2, test.CaptureCount);
    }

    [Fact]
    public void OnChanged_ThrownCaptureException_DoesNotPreventLaterCapture()
    {
        using var test = new SaveWatcherTestHarness();
        test.FailCapture = true;
        test.Signal();
        test.FailCapture = false;

        test.Signal();

        Assert.Equal(2, test.CaptureCount);
        Assert.Equal(test.Screenshot, test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public void OnChanged_UsesUpdatedLookbackOnNextAcceptedChange()
    {
        using var test = new SaveWatcherTestHarness();
        test.SecondsBeforeSave = 3;
        test.Signal();
        test.MoveTime(501);
        test.SecondsBeforeSave = 9;

        test.Signal();

        Assert.Equal(new[] { 3, 9 }, test.CaptureRequests);
    }

    [Fact]
    public void Setup_ClearsPreviousScreenshotAndResetsDebounce()
    {
        using var test = new SaveWatcherTestHarness();
        test.Signal();
        Assert.NotNull(test.Watcher.NewestSaveScreenshot);

        test.Watcher.Setup(new List<string> { test.Root });
        test.Signal();

        Assert.Equal(2, test.CaptureCount);
        Assert.Equal(test.Screenshot, test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public void Setup_ClearsOldScreenshotEvenWhenNoSourcesAreValid()
    {
        using var test = new SaveWatcherTestHarness();
        test.Signal();

        test.Watcher.Setup(new List<string>());

        Assert.Null(test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public async Task Stop_PreservesLatestScreenshotForPostGameSync()
    {
        using var test = new SaveWatcherTestHarness();
        test.Watcher.Setup(new List<string> { test.Root });
        test.Signal();
        var screenshot = test.Watcher.NewestSaveScreenshot;

        await test.Watcher.Stop();

        Assert.Same(screenshot, test.Watcher.NewestSaveScreenshot);
    }
}
