using Graviton.Models;
using Graviton.Models.Saves;
using Graviton.Tests.Fakes.RomM;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveDiscoveryRemoteTests
{
    [Fact]
    public async Task DiscoverSaves_WithNoLocalSaves_IncludesSlottedRemoteSave()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.ConfigureMappingSaves(mapping, RomMSaveFixtures.CreateSave(987, 42, "autosave", "Mario.sav"));

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        var save = Assert.Single(Assert.IsType<List<GravitonSave>>(saves));
        Assert.Equal(42, save.ROMID);
        Assert.Equal(987, save.SaveID);
        Assert.Equal("autosave", save.Slot);
        Assert.Equal(SaveStatus.ServerOnly, save.Status);
        Assert.True(save.IsCurrent);
        Assert.Equal(2048, save.FileSize);
        Assert.Equal("hash-987", save.ServerHash);
    }

    [Fact]
    public async Task DiscoverSaves_ExcludesArchivalRemoteSavesWithoutSlots()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.ConfigureMappingSaves(mapping,
            RomMSaveFixtures.CreateSave(100, 42, null),
            RomMSaveFixtures.CreateSave(101, 42, ""),
            RomMSaveFixtures.CreateSave(102, 42, "autosave"));

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Equal(102, Assert.Single(Assert.IsType<List<GravitonSave>>(saves)).SaveID);
    }

    [Fact]
    public async Task DiscoverSaves_RemovesServerTimestampTagFromFilenameAndPath()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.ConfigureMappingSaves(mapping, RomMSaveFixtures.CreateSave(987, 42, "autosave", "Mario [2026-10-10_12-00-00].sav"));

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        var save = Assert.Single(Assert.IsType<List<GravitonSave>>(saves));
        Assert.Equal("Mario.sav", save.Filename);
        Assert.Equal($"{EmulatorMapping.SavePathToken}/Mario.sav", Assert.Single(save.SourceFilePaths));
    }

    [Fact]
    public async Task DiscoverSaves_PreservesDistinctROMSlotsWithoutMerging()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.ConfigureMappingSaves(mapping,
            RomMSaveFixtures.CreateSave(10, 42, "autosave"),
            RomMSaveFixtures.CreateSave(11, 42, "slot2"),
            RomMSaveFixtures.CreateSave(12, 73, "autosave"));

        var saves = Assert.IsType<List<GravitonSave>>(await harness.Discovery.DiscoverSaves(mapping));

        Assert.Equal(3, saves.Count);
        Assert.Contains(saves, save => { return save.ROMID == 42 && save.Slot == "autosave"; });
        Assert.Contains(saves, save => { return save.ROMID == 42 && save.Slot == "slot2"; });
        Assert.Contains(saves, save => { return save.ROMID == 73 && save.Slot == "autosave"; });
    }

    [Fact]
    public async Task DiscoverSaves_ForMapping_UsesPlatformScopedRequest()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(platformId: 27);
        harness.ConfigureMappingSaves(mapping);

        await harness.Discovery.DiscoverSaves(mapping);

        await harness.RomM.Server.Received(1).GETAsync("/api/saves?platform_id=27", false);
        await harness.RomM.Server.DidNotReceive().GETAsync("/api/saves", false);
    }

    [Fact]
    public async Task DiscoverSaves_WithoutScope_UsesAllSavesRequest()
    {
        using var harness = new SaveDiscoveryTestHarness();
        harness.ConfigureAllSaves();

        var saves = await harness.Discovery.DiscoverSaves();

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
        await harness.RomM.Server.Received(1).GETAsync("/api/saves", false);
    }

    [Fact]
    public async Task DiscoverSaves_ForExplicitROMs_RequestsEachROMIndividually()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        var first = harness.AddROM(mapping, 42, imported: false);
        var second = harness.AddROM(mapping, 73, "Zelda.nes", imported: false);
        harness.ConfigureROMSaves(42, RomMSaveFixtures.CreateSave(100, 42, "autosave"));
        harness.ConfigureROMSaves(73, RomMSaveFixtures.CreateSave(101, 73, "autosave"));

        var saves = Assert.IsType<List<GravitonSave>>(await harness.Discovery.DiscoverSaves(new List<Graviton.Models.ROM.RomMRomLocal> { first, second }));

        Assert.Equal(new[] { 42, 73 }, saves.Select(save => { return save.ROMID; }).OrderBy(id => { return id; }));
        await harness.RomM.Server.Received(1).GETAsync("/api/saves?rom_id=42", false);
        await harness.RomM.Server.Received(1).GETAsync("/api/saves?rom_id=73", false);
        await harness.RomM.Server.DidNotReceive().GETAsync("/api/saves", false);
    }

    [Fact]
    public async Task DiscoverSaves_ForExplicitROMs_ContinuesWhenOneROMRequestHasNoResponse()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        var first = harness.AddROM(mapping, 42, imported: false);
        var second = harness.AddROM(mapping, 73, "Zelda.nes", imported: false);
        harness.RomM.ConfigureJsonGETResponse("/api/saves?rom_id=42", null);
        harness.ConfigureROMSaves(73, RomMSaveFixtures.CreateSave(101, 73, "autosave"));

        var saves = await harness.Discovery.DiscoverSaves(new List<Graviton.Models.ROM.RomMRomLocal> { first, second });

        Assert.Equal(73, Assert.Single(Assert.IsType<List<GravitonSave>>(saves)).ROMID);
    }

    [Fact]
    public async Task DiscoverSaves_WhenRemoteServerHasNoResponse_ReturnsEmptyListWithoutLocalSaves()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        harness.RomM.ConfigureJsonGETResponse($"/api/saves?platform_id={mapping.RomMPlatformId}", null);

        var saves = await harness.Discovery.DiscoverSaves(mapping);

        Assert.Empty(Assert.IsType<List<GravitonSave>>(saves));
    }
}
