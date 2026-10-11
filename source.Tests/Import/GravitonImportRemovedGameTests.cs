using Graviton.Import;
using Graviton.Models;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Platform;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using Playnite;

using System.IO;

using Xunit;

namespace Graviton.Tests.Import;

public sealed class GravitonImportRemovedGameTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "Graviton.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly GravitonContextFake _plugin = new();
    private readonly PlayniteFake _playnite = new();
    private readonly RomMServerFake _romm = new();
    private readonly EmulatorMapping _mapping = new()
    {
        MappingId = Guid.NewGuid(),
        Enabled = true,
        RomMPlatform = new RomMPlatform { Id = 7, Name = "Nintendo Switch", Slug = "switch" }
    };

    public GravitonImportRemovedGameTests()
    {
        Directory.CreateDirectory(Path.Combine(_testDirectory, "Games"));
        _plugin.ConfigurePluginDataDirectory(_testDirectory);
        _plugin.Settings.Mappings.Add(_mapping);
    }

    [Fact]
    public async Task RemoveMissingGames_WhenGameWasProcessed_PreservesGameAndCache()
    {
        var game = SeedGame(42);

        await CreateController().RemoveMissingGames(["42"], [_mapping]);

        Assert.NotNull(_playnite.Games.Api.Get(game.Id));
        Assert.True(_plugin.ImportedGames.ContainsKey("42"));
        Assert.True(File.Exists(GetCachePath(42)));
    }

    [Fact]
    public async Task RemoveMissingGames_WhenGameIsMissing_RemovesGameAndCache()
    {
        var game = SeedGame(42);

        await CreateController().RemoveMissingGames([], [_mapping]);

        Assert.Null(_playnite.Games.Api.Get(game.Id));
        Assert.False(_plugin.ImportedGames.ContainsKey("42"));
        Assert.False(File.Exists(GetCachePath(42)));
    }

    [Fact]
    public async Task RemoveMissingGames_WhenOnlyOneGameIsMissing_PreservesProcessedGame()
    {
        var retained = SeedGame(42);
        var removed = SeedGame(43);

        await CreateController().RemoveMissingGames(["42"], [_mapping]);

        Assert.NotNull(_playnite.Games.Api.Get(retained.Id));
        Assert.Null(_playnite.Games.Api.Get(removed.Id));
        Assert.True(_plugin.ImportedGames.ContainsKey("42"));
        Assert.False(_plugin.ImportedGames.ContainsKey("43"));
    }

    [Fact]
    public async Task RemoveMissingGames_WhenCacheKeyIsNotRomId_DoesNotDeleteGame()
    {
        var game = new Game("Unusual Cache Key")
        {
            LibraryId = GravitonPlugin.Id,
            LibraryGameId = "unparseable"
        };
        _playnite.Games.Seed(game);
        _plugin.ImportedGames.TryAdd("unparseable", new RomMRomLocal
        {
            Id = 42,
            PlayniteID = game.Id,
            MappingID = _mapping.MappingId
        });

        await CreateController().RemoveMissingGames([], [_mapping]);

        Assert.NotNull(_playnite.Games.Api.Get(game.Id));
        Assert.True(_plugin.ImportedGames.ContainsKey("unparseable"));
    }

    [Fact]
    public async Task RemoveMissingGames_WhenMappingIsDisabled_PreservesCachedGame()
    {
        var game = SeedGame(42);
        _mapping.Enabled = false;

        await CreateController().RemoveMissingGames([], [_mapping]);

        Assert.NotNull(_playnite.Games.Api.Get(game.Id));
        Assert.True(File.Exists(GetCachePath(42)));
    }

    [Fact]
    public async Task RemoveMissingGames_WhenMappingWasNotProcessed_PreservesCachedGame()
    {
        var game = SeedGame(42);

        await CreateController().RemoveMissingGames([], []);

        Assert.NotNull(_playnite.Games.Api.Get(game.Id));
        Assert.True(_plugin.ImportedGames.ContainsKey("42"));
    }

    [Fact]
    public async Task RemoveMissingGames_WhenCacheJsonIsCorrupted_PreservesGame()
    {
        var game = SeedGame(42);
        File.WriteAllText(GetCachePath(42), "{ invalid json");

        await CreateController().RemoveMissingGames([], [_mapping]);

        Assert.NotNull(_playnite.Games.Api.Get(game.Id));
        Assert.True(_plugin.ImportedGames.ContainsKey("42"));
    }

    [Fact]
    public async Task RemoveMissingGames_WhenGameOwnsRelation_RemovesRelation()
    {
        var game = SeedGame(42);
        var linked = SeedGame(43);
        var relation = new GameRelation
        {
            PrimaryGame = game.Id
        };
        relation.LinkedGames.Add(linked.Id);
        _playnite.GameRelations.Seed(relation);

        await CreateController().RemoveMissingGames(["43"], [_mapping]);

        Assert.Null(_playnite.Games.Api.Get(game.Id));
        Assert.Equal(0, _playnite.GameRelations.Api.Count);
        Assert.NotNull(_playnite.Games.Api.Get(linked.Id));
    }

    [Fact]
    public async Task RemoveMissingGames_WhenGameIsLinked_PrunesRelationWithoutRemovingPrimary()
    {
        var primary = SeedGame(42);
        var linked = SeedGame(43);
        var relation = new GameRelation
        {
            PrimaryGame = primary.Id
        };
        relation.LinkedGames.Add(linked.Id);
        _playnite.GameRelations.Seed(relation);

        await CreateController().RemoveMissingGames(["42"], [_mapping]);

        var storedRelation = Assert.Single(_playnite.GameRelations.Api);
        Assert.Equal(primary.Id, storedRelation.PrimaryGame);
        Assert.DoesNotContain(linked.Id, storedRelation.LinkedGames);
        Assert.NotNull(_playnite.Games.Api.Get(primary.Id));
        Assert.Null(_playnite.Games.Api.Get(linked.Id));
    }

    private GravitonImportController CreateController()
    {
        return new GravitonImportController(
            plugin: _plugin.Context,
            playniteAPI: _playnite.Api,
            logger: new GravitonLogger(_plugin.Settings),
            server: _romm.Server);
    }

    private Game SeedGame(int id)
    {
        var game = new Game($"ROM {id}")
        {
            LibraryId = GravitonPlugin.Id,
            LibraryGameId = id.ToString()
        };
        _playnite.Games.Seed(game);

        var local = new RomMRomLocal
        {
            Id = id,
            Name = game.Name,
            PlayniteID = game.Id,
            MappingID = _mapping.MappingId
        };
        local.Save(_plugin.Context);

        return game;
    }

    private string GetCachePath(int id)
    {
        return Path.Combine(_testDirectory, "Games", $"{id}.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
