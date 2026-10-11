using Graviton.Models;
using Graviton.Tests.Fakes.Saves;

using System.Collections.ObjectModel;
using System.IO;

using Xunit;

namespace Graviton.Tests.Saves;

[Collection("SaveManagerTransferTests")]
public sealed class SaveManagerDeleteLocalTests
{
    [Fact]
    public async Task DeleteLocalSave_WhenNothingIsTracked_DoesNothing()
    {
        using var test = new SaveManagerTestHarness();
        var unrelated = test.WriteLocalSave(new byte[] { 0x71 });
        test.ROM.LocalSave = null;

        var deleted = await test.Manager.DeleteLocalSave(test.ROM);

        Assert.False(deleted);
        Assert.Equal(new byte[] { 0x71 }, File.ReadAllBytes(unrelated));
    }

    [Fact]
    public async Task DeleteLocalSave_WhenMappingIsMissing_PreservesTrackedFiles()
    {
        using var test = new SaveManagerTestHarness();
        var path = test.WriteLocalSave(new byte[] { 0x51 });
        test.ROM.MappingID = Guid.NewGuid();

        var deleted = await test.Manager.DeleteLocalSave(test.ROM);

        Assert.False(deleted);
        Assert.Same(test.Save, test.ROM.LocalSave);
        Assert.Equal(new byte[] { 0x51 }, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task DeleteLocalSave_RemovesTrackedFileButNotUnrelatedSaves()
    {
        using var test = new SaveManagerTestHarness();
        var tracked = test.WriteLocalSave(new byte[] { 0x01 });
        var unrelated = test.WriteLocalSave(new byte[] { 0x02 }, "another-game.sav");

        var deleted = await test.Manager.DeleteLocalSave(test.ROM);

        Assert.True(deleted);
        Assert.False(File.Exists(tracked));
        Assert.True(File.Exists(unrelated));
        Assert.Null(test.ROM.LocalSave);
    }

    [Fact]
    public async Task DeleteLocalSave_RemovesEveryTrackedFileAcrossSubdirectories()
    {
        using var test = new SaveManagerTestHarness();
        var first = test.WriteLocalSave(new byte[] { 0x01 });
        var second = test.WriteLocalSave(new byte[] { 0x02 }, "profile/slot1.sav");
        var unrelated = test.WriteLocalSave(new byte[] { 0x03 }, "profile/untracked.sav");
        test.Save.SourceFilePaths = new ObservableCollection<string>
        {
            EmulatorMapping.SavePathToken + "/autosave.sav",
            EmulatorMapping.SavePathToken + "/profile/slot1.sav"
        };

        var deleted = await test.Manager.DeleteLocalSave(test.ROM);

        Assert.True(deleted);
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.True(File.Exists(unrelated));
        Assert.Null(test.ROM.LocalSave);
    }

    [Fact]
    public async Task DeleteLocalSave_WhenFolderIsTracked_RemovesOnlyThatFolder()
    {
        using var test = new SaveManagerTestHarness();
        var file = test.WriteLocalSave(new byte[] { 0x11 }, "gameA/slots/autosave.sav");
        var unrelated = test.WriteLocalSave(new byte[] { 0x22 }, "gameB/autosave.sav");
        test.Save.SourceFilePaths = new ObservableCollection<string>
        {
            EmulatorMapping.SavePathToken + "/gameA"
        };

        var deleted = await test.Manager.DeleteLocalSave(test.ROM);

        Assert.True(deleted);
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(Path.Combine(test.SavesDirectory, "gameA")));
        Assert.True(File.Exists(unrelated));
        Assert.Null(test.ROM.LocalSave);
    }

    [Fact]
    public async Task DeleteLocalSave_WhenTrackedFileIsAlreadyMissing_ClearsTracking()
    {
        using var test = new SaveManagerTestHarness();

        var deleted = await test.Manager.DeleteLocalSave(test.ROM);

        Assert.True(deleted);
        Assert.Null(test.ROM.LocalSave);
    }

    [Fact]
    public async Task DeleteLocalSave_WhenCalledTwice_SecondCallDoesNothing()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x03 });

        Assert.True(await test.Manager.DeleteLocalSave(test.ROM));
        Assert.False(await test.Manager.DeleteLocalSave(test.ROM));
    }
}
