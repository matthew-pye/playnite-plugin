using System.IO;

using Graviton.Models;
using Graviton.Models.Saves;
using Graviton.Tests.Fakes.Saves;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveDiscoveryAutoDetectionTests
{
    [Fact]
    public async Task SingleFile_MatchesROMFilenameWithoutItsExtension()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.AddROM(mapping, 42, "Mario.nes");
        var path = harness.WriteSave(mapping, "Mario.sav", "local progress");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        var save = Assert.Single(Assert.IsType<List<GravitonSave>>(saves));
        Assert.Equal(42, save.ROMID);
        Assert.Equal("Mario.sav", save.Filename);
        Assert.Equal(SaveStatus.UntrackedLocal, save.Status);
        Assert.Equal(new FileInfo(path).Length, save.FileSize);
        Assert.Equal($"{EmulatorMapping.SavePathToken}/Mario.sav", Assert.Single(save.SourceFilePaths));
        Assert.NotEmpty(Assert.IsType<System.Collections.ObjectModel.ObservableCollection<SaveDirectoryTree>>(save.SaveDirectoryTrees));
    }

    [Fact]
    public async Task SingleFile_MatchesROMAndExtensionsWithoutCaseSensitivity()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(extensions: "sav");
        harness.AddROM(mapping, 42, "Mario.NES");
        harness.WriteSave(mapping, "mArIo.SAV", "data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Equal("mArIo.SAV", Assert.Single(Assert.IsType<List<GravitonSave>>(saves)).Filename);
    }

    [Fact]
    public async Task SingleFile_UsesSaveTargetWhenItDiffersFromROMFilename()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.AddROM(mapping, 42, "Mario.nes", saveTarget: "GAME0001");
        harness.WriteSave(mapping, "GAME0001.sav", "data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Equal("GAME0001.sav", Assert.Single(Assert.IsType<List<GravitonSave>>(saves)).Filename);
    }

    [Fact]
    public async Task SingleFile_WithHexadecimalGameCubeSaveTarget_FindsGCISave()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(extensions: "gci");
        harness.AddROM(mapping, 42, "Mario.iso", saveTarget: "4D415249");
        harness.WriteSave(mapping, "01-MARI-state.gci", "memory card data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        var save = Assert.Single(Assert.IsType<List<GravitonSave>>(saves));
        Assert.Equal(42, save.ROMID);
        Assert.Equal("01-MARI-state.gci", save.Filename);
    }

    [Fact]
    public async Task SingleFile_FindsSavesWithinNestedFolders()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, Path.Combine("slot1", "Mario.sav"), "data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        var path = Assert.Single(Assert.Single(Assert.IsType<List<GravitonSave>>(saves)).SourceFilePaths);
        Assert.EndsWith("/slot1/Mario.sav", path);
    }

    [Fact]
    public async Task SingleFile_IgnoresUnrelatedROMNames()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, "Zelda.sav", "data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
    }

    [Fact]
    public async Task SingleFile_ExcludesFilesWithUnconfiguredExtensions()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(extensions: "sav");
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, "Mario.png", "screenshot");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
    }

    [Fact]
    public async Task SingleFile_WithExtensionlessSave_UsesNoneExtensionToken()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(extensions: "<none>");
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, "Mario", "raw state");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Equal("Mario", Assert.Single(Assert.IsType<List<GravitonSave>>(saves)).Filename);
    }

    [Fact]
    public async Task SingleFile_CreatesIndependentSavesForDifferentExtensions()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(extensions: "sav;srm");
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, "Mario.sav", "a");
        harness.WriteSave(mapping, "Mario.srm", "longer save");
        harness.ConfigureMappingSaves(mapping);

        var saves = Assert.IsType<List<GravitonSave>>(await harness.Discovery.DiscoverSaves(mapping));

        Assert.Equal(2, saves.Count);
        Assert.Equal(new[] { "Mario.sav", "Mario.srm" }, saves.Select(save => { return save.Filename; }).OrderBy(filename => { return filename; }));
        Assert.All(saves, save =>
        {
            Assert.Single(save.SourceFilePaths);
            Assert.Equal(42, save.ROMID);
        });
    }

    [Fact]
    public async Task SingleFile_DeduplicatesROMFilenameAndIdenticalSaveTarget()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.AddROM(mapping, 42, "Mario.nes", saveTarget: "Mario");
        harness.WriteSave(mapping, "Mario.sav", "data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Single(Assert.IsType<List<GravitonSave>>(saves));
    }

    [Fact]
    public async Task FixedSet_CombinesRelatedFilesIntoOneArchiveCandidate()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(layout: SaveLayoutStyle.FixedSet, extensions: "sav;srm");
        harness.AddROM(mapping, 42, "Mario.nes");
        var first = harness.WriteSave(mapping, "Mario.sav", "first");
        var second = harness.WriteSave(mapping, "Mario.srm", "second");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        var save = Assert.Single(Assert.IsType<List<GravitonSave>>(saves));
        Assert.Equal("Mario.zip", save.Filename);
        Assert.Equal(2, save.SourceFilePaths.Count);
        Assert.Equal(new FileInfo(first).Length + new FileInfo(second).Length, save.FileSize);
    }

    [Fact]
    public async Task FixedSet_UsesOriginalFilenameWhenOnlyOneFileMatches()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(layout: SaveLayoutStyle.FixedSet);
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, "Mario.sav", "data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Equal("Mario.sav", Assert.Single(Assert.IsType<List<GravitonSave>>(saves)).Filename);
    }

    [Fact]
    public async Task FixedSet_WhenAllCandidatesHaveWrongExtensions_DoesNotThrow()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(layout: SaveLayoutStyle.FixedSet, extensions: "sav");
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, "Mario.png", "screenshot");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
    }

    [Fact]
    public async Task SingleFile_WhenExtensionsAreBlank_DoesNotAutoDetect()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(extensions: " ");
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, "Mario.sav", "data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
    }

    [Fact]
    public async Task MissingSaveDirectory_DoesNotAbortDiscovery()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        mapping.SavePath = Path.Combine(harness.Root, "directory-that-does-not-exist");
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
    }

    [Fact]
    public async Task MissingSaveDirectory_StillReturnsAvailableRemoteSaves()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        mapping.SavePath = Path.Combine(harness.Root, "directory-that-does-not-exist");
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.ConfigureMappingSaves(mapping, Graviton.Tests.Fakes.RomM.RomMSaveFixtures.CreateSave(501, 42, "autosave"));

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Equal(501, Assert.Single(Assert.IsType<List<GravitonSave>>(saves)).SaveID);
    }

    [Fact]
    public async Task DisabledLayout_DoesNotScanSaveFiles()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(layout: SaveLayoutStyle.Disabled);
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, "Mario.sav", "data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
    }

    [Fact]
    public async Task WholeFolder_MatchesDirectoryNamedForROMAndIncludesNestedFileSizes()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(layout: SaveLayoutStyle.WholeFolder);
        harness.AddROM(mapping, 42, "Mario.nes");
        var first = harness.WriteSave(mapping, Path.Combine("Mario", "card.bin"), "first");
        var second = harness.WriteSave(mapping, Path.Combine("Mario", "nested", "progress.bin"), "second");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        var save = Assert.Single(Assert.IsType<List<GravitonSave>>(saves));
        Assert.Equal("Mario.zip", save.Filename);
        Assert.EndsWith("/Mario/", Assert.Single(save.SourceFilePaths));
        Assert.Equal(new FileInfo(first).Length + new FileInfo(second).Length, save.FileSize);
    }

    [Fact]
    public async Task WholeFolder_IgnoresUnrelatedDirectories()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(layout: SaveLayoutStyle.WholeFolder);
        harness.AddROM(mapping, 42, "Mario.nes");
        harness.WriteSave(mapping, Path.Combine("Zelda", "progress.bin"), "data");
        harness.ConfigureMappingSaves(mapping);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
    }

    [Fact]
    public async Task MappingScope_DoesNotIncludeSavesBelongingToAnotherMapping()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var selectedMapping = harness.AddMapping(platformId: 7);
        var otherMapping = harness.AddMapping(platformId: 8);
        harness.AddROM(selectedMapping, 42, "Mario.nes");
        harness.AddROM(otherMapping, 73, "Zelda.nes");
        harness.WriteSave(otherMapping, "Zelda.sav", "data");
        harness.ConfigureMappingSaves(selectedMapping);

        var saves = await harness.Discovery.DiscoverSaves(selectedMapping);

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
    }

    [Fact]
    public async Task GlobalScope_AutoDetectsAcrossMultipleMappings()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var firstMapping = harness.AddMapping(platformId: 7);
        var secondMapping = harness.AddMapping(platformId: 8);
        harness.AddROM(firstMapping, 42, "Mario.nes");
        harness.AddROM(secondMapping, 73, "Zelda.nes");
        harness.WriteSave(firstMapping, "Mario.sav", "one");
        harness.WriteSave(secondMapping, "Zelda.sav", "two");
        harness.ConfigureAllSaves();

        var saves = Assert.IsType<List<GravitonSave>>(await harness.Discovery.DiscoverSaves());

        Assert.Equal(new[] { 42, 73 }, saves.Select(save => { return save.ROMID; }).OrderBy(id => { return id; }));
    }

    [Fact]
    public async Task ExplicitROMScope_DoesNotAutoDetectOtherImportedROMs()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        var selected = harness.AddROM(mapping, 42, "Mario.nes");
        harness.AddROM(mapping, 73, "Zelda.nes");
        harness.WriteSave(mapping, "Mario.sav", "one");
        harness.WriteSave(mapping, "Zelda.sav", "two");
        harness.ConfigureROMSaves(42);

        var saves = await harness.Discovery.DiscoverSaves(new List<Graviton.Models.ROM.RomMRomLocal> { selected });

        Assert.Equal(42, Assert.Single(Assert.IsType<List<GravitonSave>>(saves)).ROMID);
    }
}
