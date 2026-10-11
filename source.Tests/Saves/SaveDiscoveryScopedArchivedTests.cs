using Graviton.Models;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Saves;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveDiscoveryScopedArchivedTests
{
    [Fact]
    public async Task GetArchivedSaves_ForMapping_RequestsUnfilteredSaveList()
    {
        var (discovery, plugin, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var mapping = CreateMapping(7);
        plugin.Settings.Mappings.Add(mapping);
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse());

        var saves = await discovery.GetArchivedSaves(mapping);

        Assert.Empty(Assert.IsType<List<RomMSave>>(saves));
        await romm.Server.Received(1).GETAsync("/api/saves", false);
        await romm.Server.DidNotReceive().GETAsync("/api/saves?platform_id=7", false);
    }

    [Fact]
    public async Task GetArchivedSaves_ForMapping_IncludesROMsBelongingToMapping()
    {
        var (discovery, plugin, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var mapping = CreateMapping(7);
        plugin.Settings.Mappings.Add(mapping);
        SaveDiscoveryArchivedTests.AddImportedROM(plugin, 42, "Matching Game", mapping.MappingId);
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, null)));

        var saves = await discovery.GetArchivedSaves(mapping);

        var archive = Assert.Single(Assert.IsType<List<RomMSave>>(saves));
        Assert.Equal("Matching Game", archive.ROMName);
    }

    [Fact]
    public async Task GetArchivedSaves_ForMapping_DoesNotIncludeOtherMappings()
    {
        var (discovery, plugin, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var mapping = CreateMapping(7);
        SaveDiscoveryArchivedTests.AddImportedROM(plugin, 42, "Other Game", Guid.NewGuid());
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, null)));

        var saves = await discovery.GetArchivedSaves(mapping);

        Assert.Empty(Assert.IsType<List<RomMSave>>(saves));
    }

    [Fact]
    public async Task GetArchivedSaves_ForMapping_DoesNotIncludeSlottedSaves()
    {
        var (discovery, plugin, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var mapping = CreateMapping(7);
        SaveDiscoveryArchivedTests.AddImportedROM(plugin, 42, "Known Game", mapping.MappingId);
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, "autosave")));

        var saves = await discovery.GetArchivedSaves(mapping);

        Assert.Empty(Assert.IsType<List<RomMSave>>(saves));
    }

    [Fact]
    public async Task GetArchivedSaves_ForMapping_WhenRomMReturnsNull_ReturnsNull()
    {
        var (discovery, _, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var mapping = CreateMapping(7);
        romm.ConfigureJsonGETResponse("/api/saves", null);

        var saves = await discovery.GetArchivedSaves(mapping);

        Assert.Null(saves);
    }

    [Fact]
    public async Task GetArchivedSaves_ForMapping_MatchesMappingIdentityRatherThanPlatformOnly()
    {
        var (discovery, plugin, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var selectedMapping = CreateMapping(7);
        var otherMapping = CreateMapping(7);
        SaveDiscoveryArchivedTests.AddImportedROM(plugin, 42, "Selected Game", selectedMapping.MappingId);
        SaveDiscoveryArchivedTests.AddImportedROM(plugin, 43, "Other Game", otherMapping.MappingId);
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, null),
            RomMSaveFixtures.CreateSave(12, 43, null)));

        var saves = await discovery.GetArchivedSaves(selectedMapping);

        var archive = Assert.Single(Assert.IsType<List<RomMSave>>(saves));
        Assert.Equal(42, archive.ROMID);
        Assert.Equal("Selected Game", archive.ROMName);
    }

    [Fact]
    public async Task GetArchivedSaves_ForROMs_RequestsUnfilteredSaveList()
    {
        var (discovery, _, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var roms = new List<RomMRomLocal>
        {
            new() { Id = 42, Name = "Game A" },
            new() { Id = 43, Name = "Game B" }
        };
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse());

        var saves = await discovery.GetArchivedSaves(roms);

        Assert.Empty(Assert.IsType<List<RomMSave>>(saves));
        await romm.Server.Received(1).GETAsync("/api/saves", false);
        await romm.Server.DidNotReceive().GETAsync("/api/saves?rom_id=42", false);
        await romm.Server.DidNotReceive().GETAsync("/api/saves?rom_id=43", false);
    }

    [Fact]
    public async Task GetArchivedSaves_ForROMs_UsesExplicitListInsteadOfImportedCache()
    {
        var (discovery, plugin, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        SaveDiscoveryArchivedTests.AddImportedROM(plugin, 42, "Cached Game");
        var roms = new List<RomMRomLocal> { new() { Id = 42, Name = "Selected Game" } };
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, null)));

        var saves = await discovery.GetArchivedSaves(roms);

        var archive = Assert.Single(Assert.IsType<List<RomMSave>>(saves));
        Assert.Equal("Selected Game", archive.ROMName);
    }

    [Fact]
    public async Task GetArchivedSaves_ForROMs_CombinesArchivesAcrossROMs()
    {
        var (discovery, _, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var roms = new List<RomMRomLocal>
        {
            new() { Id = 42, Name = "Game A" },
            new() { Id = 43, Name = "Game B" }
        };
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, null),
            RomMSaveFixtures.CreateSave(12, 43, null)));

        var saves = await discovery.GetArchivedSaves(roms);

        Assert.NotNull(saves);
        Assert.Equal(new[] { 11, 12 }, saves.Select(save => save.ID));
        Assert.Equal(new[] { "Game A", "Game B" }, saves.Select(save => save.ROMName));
    }

    [Fact]
    public async Task GetArchivedSaves_ForROMs_WhenServerReturnsNull_ReturnsNull()
    {
        var (discovery, _, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var roms = new List<RomMRomLocal> { new() { Id = 42, Name = "Unavailable" } };
        romm.ConfigureJsonGETResponse("/api/saves", null);

        var saves = await discovery.GetArchivedSaves(roms);

        Assert.Null(saves);
    }

    [Fact]
    public async Task GetArchivedSaves_ForROMs_FiltersSlottedSaves()
    {
        var (discovery, _, romm) = SaveDiscoveryArchivedTests.CreateDiscovery();
        var roms = new List<RomMRomLocal> { new() { Id = 42, Name = "Game" } };
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, "autosave"),
            RomMSaveFixtures.CreateSave(12, 42, null)));

        var saves = await discovery.GetArchivedSaves(roms);

        Assert.Equal(12, Assert.Single(Assert.IsType<List<RomMSave>>(saves)).ID);
    }

    private static EmulatorMapping CreateMapping(int platformId)
    {
        return new EmulatorMapping
        {
            MappingId = Guid.NewGuid(),
            RomMPlatformId = platformId
        };
    }
}
