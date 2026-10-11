using Graviton.Import;
using Graviton.Models;
using Graviton.Models.RomM.Collection;
using Graviton.Models.RomM.Metadata;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;

using Playnite;

using Xunit;

using static Playnite.Plugin;

namespace Graviton.Tests.Import;

public sealed class GravitonImportGameMetadataTests
{
    [Fact]
    public async Task GenerateGame_WritesSummaryToGameDescriptionCollection()
    {
        var (importer, playnite, _) = CreateImporter();
        var rom = CreateRom();
        rom.Summary = "A summary from RomM";

        var game = await importer.GenerateGame(rom);

        Assert.NotNull(game);
        var description = Assert.Single(playnite.GameDescriptions.Api);
        Assert.Equal(game.Id, description.Id);
        Assert.Equal("A summary from RomM", description.Text);
        Assert.Equal(GameDescriptionFormat.Markdown, description.Format);
    }

    [Fact]
    public async Task GenerateGame_PreservesSortingNameAndFileSize()
    {
        var (importer, _, _) = CreateImporter();
        var rom = CreateRom();
        rom.SortName = "Example, The";
        rom.FileSizeBytes = 123456UL;

        var game = await importer.GenerateGame(rom);

        Assert.NotNull(game);
        Assert.Equal("Example, The", game.SortingName);
        Assert.Equal(123456UL, game.EstimatedInstallSize);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenerateGame_MapsFavouriteCollectionToPlaynite(bool favourite)
    {
        var (importer, _, _) = CreateImporter();
        var rom = CreateRom();
        rom.Collections = favourite
            ? [new RomMCollection { Id = 1, Name = "Favorites", RomIDs = [42] }]
            : [new RomMCollection { Id = 2, Name = "Other Collection", RomIDs = [42] }];

        var game = await importer.GenerateGame(rom);

        Assert.NotNull(game);
        Assert.Equal(favourite, game.Favorite);
    }

    [Fact]
    public async Task GenerateGame_PreservesUserHiddenStatusAndRating()
    {
        var (importer, _, _) = CreateImporter();
        var rom = CreateRom();
        rom.RomUser = new RomMRomUser { Hidden = true, Rating = 8 };

        var game = await importer.GenerateGame(rom);

        Assert.NotNull(game);
        Assert.True(game.Hidden);
        Assert.Equal(80, game.UserScore);
    }

    [Fact]
    public async Task GenerateGame_NormalizesMetadataIdentifiersToLowercase()
    {
        var (importer, _, _) = CreateImporter();
        var rom = CreateRom();
        rom.Metadatum = new Metadatum
        {
            Genres = ["Role Playing"],
            Franchises = ["Example Series"],
            Gamemodes = ["Single Player"],
            Publishers = ["Example Publisher"],
            Developers = ["Example Developer"]
        };
        rom.Regions = ["North America"];

        var game = await importer.GenerateGame(rom);

        Assert.NotNull(game);
        Assert.Contains("role playing", game.GenreIds!);
        Assert.Contains("example series", game.SeriesIds!);
        Assert.Contains("single player", game.FeatureIds!);
        Assert.Contains("example publisher", game.PublisherIds!);
        Assert.Contains("example developer", game.DeveloperIds!);
        Assert.Contains("north america", game.RegionIds!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenerateGame_RespectsMetadataCollectionsSetting(bool enabled)
    {
        var (importer, _, plugin) = CreateImporter();
        plugin.Settings.AddMetadataProviderCollections = enabled;
        var rom = CreateRom();
        rom.Metadatum = new Metadatum { Collections = ["My Collection"] };

        var game = await importer.GenerateGame(rom);

        Assert.NotNull(game);
        Assert.Equal(enabled, game.CategoryIds!.Contains("my collection"));
    }

    [Fact]
    public async Task GenerateGame_UsesRomMIdInExternalIdentifierAndLink()
    {
        var (importer, _, plugin) = CreateImporter();
        plugin.Settings.Host = "https://romm.example.test/";

        var game = await importer.GenerateGame(CreateRom());

        Assert.NotNull(game);
        Assert.Contains(game.ExternalIdentifiers!, id => id.TypeId == "romm" && id.IdValue == "42");
        Assert.Contains(game.Links!, link => link.TypeId == "romm" && link.Url == "https://romm.example.test/rom/42");
    }

    [Fact]
    public async Task GenerateGame_ProvidesExternalLinksForKnownMetadataProviders()
    {
        var (importer, _, _) = CreateImporter();
        var rom = CreateRom();
        rom.IgdbId = 100;
        rom.Slug = "an-example-game";
        rom.RAId = 200;
        rom.SSId = 300;

        var game = await importer.GenerateGame(rom);

        Assert.NotNull(game);
        Assert.Contains(game.ExternalIdentifiers!, id => id.TypeId == "igdb" && id.IdValue == "100");
        Assert.Contains(game.ExternalIdentifiers!, id => id.TypeId == "romm" && id.IdValue == "42");
        Assert.Contains(game.Links!, link => link.TypeId == "igdb" && link.Url == "https://www.igdb.com/games/an-example-game");
        Assert.Contains(game.Links!, link => link.TypeId == "retroachievements");
        Assert.Contains(game.Links!, link => link.TypeId == "screenscraper");
    }

    [Fact]
    public async Task GenerateGame_MapsUserCompletionStatusToExistingPlayniteStatus()
    {
        var (importer, playnite, _) = CreateImporter();
        var completion = new CompletionStatus("Playing");
        playnite.CompletionStatuses.Seed(completion);
        var rom = CreateRom();
        rom.RomUser = new RomMRomUser { Status = "now_playing" };

        var game = await importer.GenerateGame(rom);

        Assert.NotNull(game);
        Assert.Equal(completion.Id, game.CompletionStatusId);
    }

    [Fact]
    public async Task GenerateGame_WithNoOptionalMetadata_DoesNotInventFavouritesOrTags()
    {
        var (importer, _, _) = CreateImporter();

        var game = await importer.GenerateGame(CreateRom());

        Assert.NotNull(game);
        Assert.False(game.Favorite);
        Assert.False(game.Hidden);
        Assert.Empty(game.CategoryIds!);
    }

    private static (GravitonImport Importer, PlayniteFake Playnite, GravitonContextFake Plugin) CreateImporter()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var mapping = new EmulatorMapping
        {
            MappingId = Guid.NewGuid(),
            RomMPlatform = new RomMPlatform { Id = 17, Name = "Nintendo 64" },
            DestinationPath = "C:/Games/N64"
        };
        var importer = new GravitonImport(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            args: new ImportGamesArgs(),
            mapping: mapping);

        return (importer, playnite, plugin);
    }

    private static RomMRom CreateRom()
    {
        return new RomMRom
        {
            Id = 42,
            Name = "Example Game",
            FileName = "example.z64",
            FileNameNoExt = "example",
            FileSizeBytes = 4096,
            HasMultipleFiles = true,
            PlatformId = 17
        };
    }
}
