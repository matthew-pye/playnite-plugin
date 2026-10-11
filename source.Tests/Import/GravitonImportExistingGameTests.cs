using Graviton.Import;
using Graviton.Models;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Collection;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;

using Playnite;

using System.IO;
using System.Text.Json;

using Xunit;

using static Playnite.Plugin;

namespace Graviton.Tests.Import;

public sealed class GravitonImportExistingGameTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "Graviton.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly GravitonContextFake _plugin = new();
    private readonly PlayniteFake _playnite = new();
    private readonly EmulatorMapping _mapping = new()
    {
        MappingId = Guid.NewGuid(),
        RomMPlatform = new RomMPlatform { Id = 7, Name = "Nintendo Switch", Slug = "switch" }
    };

    public GravitonImportExistingGameTests()
    {
        Directory.CreateDirectory(Path.Combine(_testDirectory, "Games"));
        _plugin.ConfigurePluginDataDirectory(_testDirectory);
        _plugin.Settings.Host = "https://romm.example";
        _plugin.Settings.MergeRevisions = false;
        _plugin.Settings.KeepDeletedGames = false;
    }

    [Fact]
    public async Task ProcessData_WhenRomAlreadyExists_DoesNotCreateDuplicateGame()
    {
        var game = SeedGame(42);
        var result = await CreateImporter().ProcessData([CreateRom(42)]);

        Assert.Empty(result.NewGames);
        Assert.Equal(new[] { "42" }, result.ProcessedGames);
        Assert.Equal(1, _playnite.Games.Api.Count);
        Assert.NotNull(_playnite.Games.Api.Get(game.Id));
    }

    [Fact]
    public async Task ProcessData_WhenRomChanges_PreservesUserEditedPlayniteFields()
    {
        var game = new Game("My Custom Title")
        {
            SortingName = "Custom Sort Name",
            PlayTime = 3600,
            UserScore = 90,
            Hidden = true,
            InstallState = InstallState.Installed,
            Favorite = true
        };
        SeedGame(42, game);

        await CreateImporter().ProcessData([CreateRom(42, "Server Name", "revision-2.rom")]);

        var stored = _playnite.Games.Api.Get(game.Id);
        Assert.NotNull(stored);
        Assert.Equal("My Custom Title", stored.Name);
        Assert.Equal("Custom Sort Name", stored.SortingName);
        Assert.Equal((uint)3600, stored.PlayTime);
        Assert.Equal(90, stored.UserScore);
        Assert.True(stored.Hidden);
        Assert.Equal(InstallState.Installed, stored.InstallState);
        Assert.True(stored.Favorite);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessData_WhenRomCollectionsArePresent_UpdatesFavouriteStatus(bool favourite)
    {
        var game = new Game("User Title") { Favorite = !favourite };
        SeedGame(42, game);
        var rom = CreateRom(42);
        rom.Collections = favourite
            ? [new RomMCollection { Name = "Favorites", RomIDs = [42] }]
            : [new RomMCollection { Name = "Backlog", RomIDs = [42] }];

        await CreateImporter().ProcessData([rom]);

        var stored = _playnite.Games.Api.Get(game.Id);
        Assert.NotNull(stored);
        Assert.Equal(favourite, stored.Favorite);
    }

    [Fact]
    public async Task ProcessData_WhenRomCollectionsAreMissing_PreservesFavouriteStatus()
    {
        var game = new Game("User Title") { Favorite = true };
        SeedGame(42, game);
        var rom = CreateRom(42);
        rom.Collections = null;

        await CreateImporter().ProcessData([rom]);

        var stored = _playnite.Games.Api.Get(game.Id);
        Assert.NotNull(stored);
        Assert.True(stored.Favorite);
    }

    [Fact]
    public async Task ProcessData_WhenHostChanges_UpdatesRomMLinkButPreservesOtherLinks()
    {
        var game = new Game("User Title") { Links = new() };
        game.Links!.Add(new WebLink("romm", "https://previous.example/rom/42"));
        game.Links!.Add(new WebLink("community", "https://example.org/wikis/42"));
        SeedGame(42, game);

        await CreateImporter().ProcessData([CreateRom(42)]);

        var stored = _playnite.Games.Api.Get(game.Id);
        Assert.NotNull(stored);
        Assert.Contains(stored.Links!, link => link.TypeId == "romm" && link.Url == "https://romm.example/rom/42");
        Assert.Contains(stored.Links!, link => link.TypeId == "community" && link.Url == "https://example.org/wikis/42");
    }

    [Fact]
    public async Task ProcessData_WhenCachedPlayniteIdIsStale_RecoversExistingGame()
    {
        var game = SeedGame(42);
        _plugin.ImportedGames["42"].PlayniteID = "outdated-playnite-id";

        var result = await CreateImporter().ProcessData([CreateRom(42)]);

        Assert.Empty(result.NewGames);
        Assert.Equal(1, _playnite.Games.Api.Count);
        Assert.Equal(game.Id, _plugin.ImportedGames["42"].PlayniteID);

        var saved = ReadLocalROM(42);
        Assert.Equal(game.Id, saved.PlayniteID);
    }

    [Fact]
    public async Task ProcessData_WhenRomFilenameChanges_PersistsUpdatedLocalRomMetadata()
    {
        var game = SeedGame(42);
        var rom = CreateRom(42, "New RomM Title", "new-version.rom");
        rom.SHA1 = new string('b', 40);

        var result = await CreateImporter().ProcessData([rom]);

        Assert.Empty(result.NewGames);
        Assert.True(rom.Processed);
        Assert.Equal(game.Id, _plugin.ImportedGames["42"].PlayniteID);

        var saved = ReadLocalROM(42);
        Assert.Equal("New RomM Title", saved.Name);
        Assert.Equal("new-version.rom", saved.FileName);
        Assert.Equal(new string('b', 40), saved.SHA1);
        Assert.Equal("/api/roms/42/content/new-version.rom", saved.DownloadURL);
    }

    [Fact]
    public async Task ProcessData_WithTwoExistingRoms_UpdatesEachWithoutAddingGames()
    {
        var first = SeedGame(42);
        var second = SeedGame(43);

        var result = await CreateImporter().ProcessData([
            CreateRom(42, "Updated First", "first.rom"),
            CreateRom(43, "Updated Second", "second.rom")
        ]);

        Assert.Empty(result.NewGames);
        Assert.Equal(new[] { "42", "43" }, result.ProcessedGames);
        Assert.Equal(2, _playnite.Games.Api.Count);
        Assert.NotNull(_playnite.Games.Api.Get(first.Id));
        Assert.NotNull(_playnite.Games.Api.Get(second.Id));
        Assert.Equal("Updated First", ReadLocalROM(42).Name);
        Assert.Equal("Updated Second", ReadLocalROM(43).Name);
    }

    private GravitonImport CreateImporter()
    {
        return new GravitonImport(
            _plugin.Context,
            _playnite.Api,
            new GravitonLogger(_plugin.Settings),
            new ImportGamesArgs { SessionImport = SessionImportMode.Never },
            _mapping);
    }

    private Game SeedGame(int romId, Game? game = null)
    {
        game ??= new Game($"Playnite ROM {romId}");
        game.LibraryId = GravitonPlugin.Id;
        game.LibraryGameId = romId.ToString();
        _playnite.Games.Seed(game);

        _plugin.ImportedGames.TryAdd(romId.ToString(), new RomMRomLocal
        {
            Id = romId,
            Name = "Old RomM Title",
            PlayniteID = game.Id,
            FileName = "old-version.rom",
            SHA1 = new string('a', 40),
            MappingID = _mapping.MappingId
        });

        return game;
    }

    private static RomMRom CreateRom(int id, string name = "Current RomM Title", string fileName = "current.rom")
    {
        return new RomMRom
        {
            Id = id,
            Name = name,
            FileName = fileName,
            FileNameNoExt = Path.GetFileNameWithoutExtension(fileName),
            SHA1 = new string('a', 40),
            HasSimpleSingleFile = true,
            FileSystemPath = "/roms/",
            FullPath = "/roms/" + fileName,
            Files =
            [
                new RomMFile
                {
                    Id = id + 100,
                    FileName = fileName,
                    FullPath = "/roms/" + fileName,
                    Category = RomMCategory.Game
                }
            ]
        };
    }

    private RomMRomLocal ReadLocalROM(int romId)
    {
        var path = Path.Combine(_testDirectory, "Games", $"{romId}.json");
        Assert.True(File.Exists(path));

        var rom = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText(path));
        return Assert.IsType<RomMRomLocal>(rom);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
