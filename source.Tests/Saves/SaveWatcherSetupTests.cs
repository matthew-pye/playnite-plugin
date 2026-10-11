using Graviton.Tests.Fakes.Saves;

using System.IO;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveWatcherSetupTests
{
    [Fact]
    public void Setup_EmptySources_ReturnsFalse()
    {
        using var test = new SaveWatcherTestHarness();

        var result = test.Watcher.Setup(new List<string>());

        Assert.False(result);
        Assert.Empty(test.FileWatchers);
    }

    [Fact]
    public void Setup_ExistingFile_WatchesOnlyThatFilename()
    {
        using var test = new SaveWatcherTestHarness();
        var file = test.WriteFile();

        var result = test.Watcher.Setup(new List<string> { file });

        Assert.True(result);
        var watcher = Assert.Single(test.FileWatchers);
        Assert.Equal(test.Root, watcher.Path);
        Assert.Equal("progress.sav", watcher.Filter);
        Assert.False(watcher.IncludeSubdirectories);
        Assert.Equal(NotifyFilters.LastWrite | NotifyFilters.FileName, watcher.NotifyFilter);
    }

    [Fact]
    public void Setup_ExistingDirectory_WatchesDescendants()
    {
        using var test = new SaveWatcherTestHarness();

        var result = test.Watcher.Setup(new List<string> { test.Root });

        Assert.True(result);
        var watcher = Assert.Single(test.FileWatchers);
        Assert.Equal(test.Root, watcher.Path);
        Assert.True(watcher.IncludeSubdirectories);
        Assert.Contains(watcher.Filter, new[] { "*", "*.*" });
    }

    [Fact]
    public void Setup_FileNotYetCreated_WatchesItsParentDirectory()
    {
        using var test = new SaveWatcherTestHarness();
        var file = test.FilePath("future.sav");

        var result = test.Watcher.Setup(new List<string> { file });

        Assert.True(result);
        var watcher = Assert.Single(test.FileWatchers);
        Assert.Equal(test.Root, watcher.Path);
        Assert.Equal("future.sav", watcher.Filter);
        Assert.False(watcher.IncludeSubdirectories);
    }

    [Fact]
    public void Setup_ExtensionlessFileNotYetCreated_IsWatchable()
    {
        using var test = new SaveWatcherTestHarness();
        var file = test.FilePath("SRAM");

        Assert.True(test.Watcher.Setup(new List<string> { file }));
        Assert.Equal("SRAM", Assert.Single(test.FileWatchers).Filter);
    }

    [Fact]
    public void Setup_NonexistentParentDirectory_DoesNotCreateWatcher()
    {
        using var test = new SaveWatcherTestHarness();
        var path = test.FilePath(Path.Combine("absent", "progress.sav"));

        Assert.False(test.Watcher.Setup(new List<string> { path }));
        Assert.Empty(test.FileWatchers);
    }

    [Fact]
    public void Setup_NonexistentDirectoryWithTrailingSeparator_DoesNotCreateFileWatcher()
    {
        using var test = new SaveWatcherTestHarness();
        var path = test.FilePath("absent") + Path.DirectorySeparatorChar;

        Assert.False(test.Watcher.Setup(new List<string> { path }));
        Assert.Empty(test.FileWatchers);
    }

    [Fact]
    public void Setup_BlankPathsAreIgnored()
    {
        using var test = new SaveWatcherTestHarness();
        var file = test.WriteFile();

        Assert.True(test.Watcher.Setup(new List<string> { "", "  ", file }));
        Assert.Single(test.FileWatchers);
    }

    [Fact]
    public void Setup_MultipleDistinctFiles_CreatesSeparateWatchers()
    {
        using var test = new SaveWatcherTestHarness();
        var first = test.WriteFile("one.sav");
        var second = test.WriteFile("two.sav");

        Assert.True(test.Watcher.Setup(new List<string> { first, second }));
        Assert.Equal(2, test.FileWatchers.Count);
        Assert.Equal(new[] { "one.sav", "two.sav" }, test.FileWatchers.Select(watcher => { return watcher.Filter; }));
    }

    [Fact]
    public void Setup_DuplicateSourcePaths_CreatesSingleWatcher()
    {
        using var test = new SaveWatcherTestHarness();
        var file = test.WriteFile();

        Assert.True(test.Watcher.Setup(new List<string> { file, file }));
        Assert.Single(test.FileWatchers);
    }

    [Fact]
    public void Setup_DuplicatePathsWithDifferentCase_CreatesSingleWatcher()
    {
        using var test = new SaveWatcherTestHarness();
        var file = test.WriteFile("progress.sav");

        Assert.True(test.Watcher.Setup(new List<string> { file, file.ToUpperInvariant() }));
        Assert.Single(test.FileWatchers);
    }

    [Fact]
    public void Setup_RepeatConfiguration_ReplacesOldWatchers()
    {
        using var test = new SaveWatcherTestHarness();
        var first = test.WriteFile("first.sav");
        var second = test.WriteFile("second.sav");
        Assert.True(test.Watcher.Setup(new List<string> { first }));
        var previous = Assert.Single(test.FileWatchers);

        Assert.True(test.Watcher.Setup(new List<string> { second }));

        Assert.Equal("second.sav", Assert.Single(test.FileWatchers).Filter);
        Assert.False(previous.EnableRaisingEvents);
    }

    [Fact]
    public void Setup_ReconfigurationWithNoValidPaths_RemovesOldWatchers()
    {
        using var test = new SaveWatcherTestHarness();
        test.Watcher.Setup(new List<string> { test.WriteFile() });
        var old = Assert.Single(test.FileWatchers);

        Assert.False(test.Watcher.Setup(new List<string>()));

        Assert.Empty(test.FileWatchers);
        Assert.False(old.EnableRaisingEvents);
    }

    [Fact]
    public async Task Start_WithoutWatchers_CompletesSuccessfully()
    {
        using var test = new SaveWatcherTestHarness();

        await test.Watcher.Start();

        Assert.Empty(test.FileWatchers);
    }

    [Fact]
    public async Task Start_EnablesAllConfiguredWatchers()
    {
        using var test = new SaveWatcherTestHarness();
        test.Watcher.Setup(new List<string> { test.WriteFile("a.sav"), test.WriteFile("b.sav") });

        await test.Watcher.Start();

        Assert.All(test.FileWatchers, watcher =>
        {
            Assert.True(watcher.EnableRaisingEvents);
        });
    }

    [Fact]
    public async Task Stop_DisposesWatchersAndClearsList()
    {
        using var test = new SaveWatcherTestHarness();
        test.Watcher.Setup(new List<string> { test.WriteFile() });
        await test.Watcher.Start();
        var old = Assert.Single(test.FileWatchers);

        await test.Watcher.Stop();

        Assert.Empty(test.FileWatchers);
        Assert.False(old.EnableRaisingEvents);
    }

    [Fact]
    public async Task Stop_CanBeCalledTwice()
    {
        using var test = new SaveWatcherTestHarness();
        test.Watcher.Setup(new List<string> { test.WriteFile() });

        await test.Watcher.Stop();
        await test.Watcher.Stop();

        Assert.Empty(test.FileWatchers);
    }

    [Fact]
    public async Task Setup_AfterStop_AllowsAnotherSession()
    {
        using var test = new SaveWatcherTestHarness();
        test.Watcher.Setup(new List<string> { test.WriteFile("old.sav") });
        await test.Watcher.Start();
        await test.Watcher.Stop();

        Assert.True(test.Watcher.Setup(new List<string> { test.WriteFile("new.sav") }));
        await test.Watcher.Start();

        Assert.True(Assert.Single(test.FileWatchers).EnableRaisingEvents);
    }
}
