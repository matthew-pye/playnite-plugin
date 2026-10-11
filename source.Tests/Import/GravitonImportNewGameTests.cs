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

public sealed class GravitonImportNewGameTests : IDisposable
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

    public GravitonImportNewGameTests()
    {
        Directory.CreateDirectory(Path.Combine(_testDirectory, "Games"));
        _plugin.ConfigurePluginDataDirectory(_testDirectory);
        _plugin.Settings.Host = "https://romm.example";
        _plugin.Settings.MergeRevisions = false;
        _plugin.Settings.KeepDeletedGames = false;
    }

    [Fact]
    public async Task ProcessData_WithNewRom_AddsGameAndCachesRom()
    {
        var result = await CreateImporter().ProcessData([CreateRom(42)]);

        var game = Assert.Single(result.NewGames);
        Assert.Equal("42", game.LibraryGameId);
        Assert.Equal(new[] { "42" }, result.ProcessedGames);
        Assert.Equal(1, _playnite.Games.Api.Count);
        Assert.NotNull(_playnite.Games.Api.Get(game.Id));
        Assert.Equal(game.Id, _plugin.ImportedGames["42"].PlayniteID);
        Assert.True(File.Exists(Path.Combine(_testDirectory, "Games", "42.json")));
    }

    [Fact]
    public async Task ProcessData_WithTwoNewRoms_CreatesDistinctGames()
    {
        var result = await CreateImporter().ProcessData([
            CreateRom(42, "First Game", "first.nsp"),
            CreateRom(43, "Second Game", "second.nsp")
        ]);

        Assert.Equal(2, result.NewGames.Count);
        Assert.Equal(new[] { "42", "43" }, result.ProcessedGames);
        Assert.Equal(2, _playnite.Games.Api.Count);
        Assert.Equal(2, _plugin.ImportedGames.Count);
        Assert.Equal(2, result.NewGames.Select(game => game.Id).Distinct().Count());
    }

    [Fact]
    public async Task ProcessData_WithRepeatedRomId_DoesNotAddDuplicateGame()
    {
        var result = await CreateImporter().ProcessData([
            CreateRom(42, "Original", "original.nsp"),
            CreateRom(42, "Revised", "revised.nsp")
        ]);

        Assert.Single(result.NewGames);
        Assert.Equal(1, _playnite.Games.Api.Count);
        Assert.Single(_plugin.ImportedGames);
        Assert.Equal("revised.nsp", ReadLocalROM(42).FileName);
    }

    [Fact]
    public async Task ProcessData_WithSingleFile_PersistsDownloadMetadata()
    {
        var result = await CreateImporter().ProcessData([CreateRom(42, "Game", "game (USA).nsp")]);

        var game = Assert.Single(result.NewGames);
        var local = ReadLocalROM(42);

        Assert.Equal(42, local.Id);
        Assert.Equal(game.Id, local.PlayniteID);
        Assert.Equal(_mapping.MappingId, local.MappingID);
        Assert.Equal("game (USA).nsp", local.FileName);
        Assert.Equal("/api/roms/42/content/game%20%28USA%29.nsp", local.DownloadURL);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-sha1")]
    public async Task ProcessData_WithMissingOrInvalidSha1_GeneratesValidSha1(string? sha1)
    {
        var rom = CreateRom(42);
        rom.SHA1 = sha1;

        await CreateImporter().ProcessData([rom]);

        Assert.NotNull(rom.SHA1);
        Assert.Equal(40, rom.SHA1.Length);
        Assert.Equal(rom.SHA1, ReadLocalROM(42).SHA1);
    }

    [Fact]
    public async Task ProcessData_WhenRomDoesNotDeclareFileLayout_DefaultsToMultipleFiles()
    {
        var rom = CreateRom(42);
        rom.HasSimpleSingleFile = false;
        rom.HasNestedSingleFile = false;
        rom.HasMultipleFiles = false;

        await CreateImporter().ProcessData([rom]);

        Assert.True(rom.HasMultipleFiles);
        Assert.True(ReadLocalROM(42).HasMultipleFiles);
    }

    [Fact]
    public async Task ProcessData_WithMatchingCollections_CreatesOnlyApplicableCategory()
    {
        var matching = new RomMCollection { Name = "My Favorites", RomIDs = [42] };
        var unrelated = new RomMCollection { Name = "Other Platform", RomIDs = [999] };

        var result = await CreateImporter().ProcessData(
            [CreateRom(42)],
            collections: [matching, unrelated]);

        var game = Assert.Single(result.NewGames);
        Assert.Contains("my favorites", game.CategoryIds!);
        Assert.Contains(_playnite.Categories.Api, category => category.Id == "my favorites");
        Assert.DoesNotContain(_playnite.Categories.Api, category => category.Id == "other platform");
    }

    [Fact]
    public async Task ProcessData_WithSummary_PersistsMarkdownDescription()
    {
        var rom = CreateRom(42);
        rom.Summary = "An original description";

        var result = await CreateImporter().ProcessData([rom]);

        var game = Assert.Single(result.NewGames);
        var description = Assert.Single(_playnite.GameDescriptions.Api);
        Assert.Equal(game.Id, description.Id);
        Assert.Equal("An original description", description.Text);
    }

    [Fact]
    public async Task ProcessData_WithEmptyRomList_CreatesNoGames()
    {
        var result = await CreateImporter().ProcessData([]);

        Assert.Empty(result.NewGames);
        Assert.Empty(result.ProcessedGames);
        Assert.Empty(_plugin.ImportedGames);
        Assert.Equal(0, _playnite.Games.Api.Count);
    }

    private GravitonImport CreateImporter()
    {
        return new GravitonImport(
            plugin: _plugin.Context,
            playniteAPI: _playnite.Api,
            logger: new GravitonLogger(_plugin.Settings),
            args: new ImportGamesArgs { SessionImport = SessionImportMode.Never },
            mapping: _mapping);
    }

    private static RomMRom CreateRom(int id, string name = "Current Game", string fileName = "current.nsp")
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

    private RomMRomLocal ReadLocalROM(int id)
    {
        var path = Path.Combine(_testDirectory, "Games", $"{id}.json");
        Assert.True(File.Exists(path));

        var local = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText(path));
        return Assert.IsType<RomMRomLocal>(local);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
