using Graviton.Tests.Fakes.Saves;

using System.IO;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveWatcherFileSystemTests
{
    [Fact]
    public async Task ExistingFile_ChangedOnDisk_TriggersCapture()
    {
        using var test = new SaveWatcherTestHarness();
        var path = test.WriteFile();
        Assert.True(test.Watcher.Setup(new List<string> { path }));
        await test.Watcher.Start();

        File.AppendAllText(path, "next-state");
        await test.WaitForCaptureAsync();

        Assert.Equal(test.Screenshot, test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public async Task MissingFile_CreatedAfterStart_TriggersCapture()
    {
        using var test = new SaveWatcherTestHarness();
        var path = test.FilePath("first-created.sav");
        Assert.True(test.Watcher.Setup(new List<string> { path }));
        await test.Watcher.Start();

        File.WriteAllText(path, "first-state");
        await test.WaitForCaptureAsync();

        Assert.Equal(test.Screenshot, test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public async Task Directory_NewFileInNestedFolder_TriggersCapture()
    {
        using var test = new SaveWatcherTestHarness();
        Directory.CreateDirectory(Path.Combine(test.Root, "slot1"));
        Assert.True(test.Watcher.Setup(new List<string> { test.Root }));
        await test.Watcher.Start();

        test.WriteFile(Path.Combine("slot1", "progress.sav"));
        await test.WaitForCaptureAsync();

        Assert.Equal(test.Screenshot, test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public async Task RenamedFile_ToTrackedFilename_TriggersCapture()
    {
        using var test = new SaveWatcherTestHarness();
        var temporaryPath = test.WriteFile("temporary.bin");
        var targetPath = test.FilePath("renamed.sav");
        Assert.True(test.Watcher.Setup(new List<string> { targetPath }));
        await test.Watcher.Start();

        File.Move(temporaryPath, targetPath);
        await test.WaitForCaptureAsync();

        Assert.Equal(test.Screenshot, test.Watcher.NewestSaveScreenshot);
    }

    [Fact]
    public async Task SessionRestart_TracksTheNextConfiguredFile()
    {
        using var test = new SaveWatcherTestHarness();
        var first = test.WriteFile("first.sav");
        Assert.True(test.Watcher.Setup(new List<string> { first }));
        await test.Watcher.Start();
        File.AppendAllText(first, "1");
        await test.WaitForCaptureAsync();
        await test.Watcher.Stop();
        var second = test.WriteFile("second.sav");
        test.ResetCaptureSignal();
        test.MoveTime(501);

        Assert.True(test.Watcher.Setup(new List<string> { second }));
        await test.Watcher.Start();
        File.AppendAllText(second, "2");
        await test.WaitForCaptureAsync();

        Assert.Equal(test.Screenshot, test.Watcher.NewestSaveScreenshot);
        Assert.Single(test.FileWatchers);
        Assert.Equal("second.sav", test.FileWatchers[0].Filter);
    }
}
