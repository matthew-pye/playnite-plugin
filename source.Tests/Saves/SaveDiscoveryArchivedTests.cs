using Graviton.Models.ROM;
using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveDiscoveryArchivedTests
{
    [Fact]
    public async Task GetArchivedSaves_WhenServerReturnsEmptyList_ReturnsEmptyList()
    {
        var (discovery, _, romm) = CreateDiscovery();
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse());

        var saves = await discovery.GetArchivedSaves();

        Assert.Empty(Assert.IsType<List<Graviton.Models.RomM.Saves.RomMSave>>(saves));
    }

    [Fact]
    public async Task GetArchivedSaves_WhenServerReturnsNull_ReturnsNull()
    {
        var (discovery, _, romm) = CreateDiscovery();
        romm.ConfigureJsonGETResponse("/api/saves", null);

        var saves = await discovery.GetArchivedSaves();

        Assert.Null(saves);
    }

    [Fact]
    public async Task GetArchivedSaves_ExcludesSlottedSaves()
    {
        var (discovery, plugin, romm) = CreateDiscovery();
        AddImportedROM(plugin, 42, "Super Mario World");
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(1, 42, "autosave"),
            RomMSaveFixtures.CreateSave(2, 42, null)));

        var saves = await discovery.GetArchivedSaves();

        var archived = Assert.Single(Assert.IsType<List<Graviton.Models.RomM.Saves.RomMSave>>(saves));
        Assert.Equal(2, archived.ID);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetArchivedSaves_IncludesBothNullAndEmptySlots(string? slot)
    {
        var (discovery, plugin, romm) = CreateDiscovery();
        AddImportedROM(plugin, 42, "Super Mario World");
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(12, 42, slot)));

        var saves = await discovery.GetArchivedSaves();

        var archive = Assert.Single(Assert.IsType<List<Graviton.Models.RomM.Saves.RomMSave>>(saves));
        Assert.Equal(slot, archive.Slot);
    }

    [Fact]
    public async Task GetArchivedSaves_ExcludesROMsNotInImportedLibrary()
    {
        var (discovery, plugin, romm) = CreateDiscovery();
        AddImportedROM(plugin, 42, "Known ROM");
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, null),
            RomMSaveFixtures.CreateSave(12, 73, null)));

        var saves = await discovery.GetArchivedSaves();

        var archive = Assert.Single(Assert.IsType<List<Graviton.Models.RomM.Saves.RomMSave>>(saves));
        Assert.Equal(42, archive.ROMID);
    }

    [Fact]
    public async Task GetArchivedSaves_UsesLocalROMName()
    {
        var (discovery, plugin, romm) = CreateDiscovery();
        AddImportedROM(plugin, 42, "Locally Renamed Game");
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, null)));

        var saves = await discovery.GetArchivedSaves();

        var archive = Assert.Single(Assert.IsType<List<Graviton.Models.RomM.Saves.RomMSave>>(saves));
        Assert.Equal("Locally Renamed Game", archive.ROMName);
    }

    [Fact]
    public async Task GetArchivedSaves_PreservesServerMetadata()
    {
        var (discovery, plugin, romm) = CreateDiscovery();
        AddImportedROM(plugin, 42, "Known ROM");
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(987, 42, null, "backup.zip")));

        var saves = await discovery.GetArchivedSaves();

        var archive = Assert.Single(Assert.IsType<List<Graviton.Models.RomM.Saves.RomMSave>>(saves));
        Assert.Equal(987, archive.ID);
        Assert.Equal("backup.zip", archive.FileName);
        Assert.Equal(2048, archive.FileSize);
        Assert.Equal("hash-987", archive.ContentHash);
        Assert.StartsWith("/api/saves/987/content?timestamp=", archive.DownloadPath);
        Assert.Contains("&name=backup.zip", archive.DownloadPath);
    }

    [Fact]
    public async Task GetArchivedSaves_ReturnsAllArchivesForTheSameROM()
    {
        var (discovery, plugin, romm) = CreateDiscovery();
        AddImportedROM(plugin, 42, "Known ROM");
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, null),
            RomMSaveFixtures.CreateSave(12, 42, null),
            RomMSaveFixtures.CreateSave(13, 42, "autosave")));

        var saves = await discovery.GetArchivedSaves();

        Assert.Equal(new[] { 11, 12 }, Assert.IsType<List<Graviton.Models.RomM.Saves.RomMSave>>(saves).Select(save => save.ID));
    }

    [Fact]
    public async Task GetArchivedSaves_PreservesDifferentROMNames()
    {
        var (discovery, plugin, romm) = CreateDiscovery();
        AddImportedROM(plugin, 42, "Game A");
        AddImportedROM(plugin, 43, "Game B");
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(11, 42, null),
            RomMSaveFixtures.CreateSave(12, 43, null)));

        var saves = await discovery.GetArchivedSaves();

        Assert.Equal(new[] { "Game A", "Game B" }, Assert.IsType<List<Graviton.Models.RomM.Saves.RomMSave>>(saves).Select(save => save.ROMName));
    }

    [Fact]
    public async Task GetArchivedSaves_RequestsUnfilteredSaveList()
    {
        var (discovery, _, romm) = CreateDiscovery();
        romm.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse());

        await discovery.GetArchivedSaves();

        await romm.Server.Received(1).GETAsync("/api/saves", false);
    }

    internal static (SaveDiscovery Discovery, GravitonContextFake Plugin, RomMServerFake RomM) CreateDiscovery()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var discovery = new SaveDiscovery(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            romMServer: romm.Server);

        return (discovery, plugin, romm);
    }

    internal static RomMRomLocal AddImportedROM(GravitonContextFake plugin, int id, string name, Guid? mappingId = null)
    {
        var rom = new RomMRomLocal
        {
            Id = id,
            Name = name,
            MappingID = mappingId ?? Guid.Empty
        };
        plugin.ImportedGames[id.ToString()] = rom;
        return rom;
    }
}
