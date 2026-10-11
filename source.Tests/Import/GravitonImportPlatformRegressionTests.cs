using Graviton.Import;
using Graviton.Models;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;

using Xunit;

using static Playnite.Plugin;

namespace Graviton.Tests.Import;

public sealed class GravitonImportPlatformRegressionTests
{
    [Theory]
    [InlineData("Nintendo Entertainment System", "nintendo entertainment system")]
    [InlineData("Super Nintendo", "super nintendo")]
    [InlineData("Nintendo 64", "nintendo 64")]
    [InlineData("Game Boy Color", "game boy color")]
    [InlineData("PlayStation Portable", "playstation portable")]
    public async Task GenerateGame_AssignsTheConfiguredPlatform(string platformName, string expectedId)
    {
        var (importer, _) = CreateImporter(new RomMPlatform { Id = 17, Name = platformName });

        var game = await importer.GenerateGame(CreateRom(42, 17));

        Assert.NotNull(game);
        Assert.Equal(expectedId, Assert.Single(game.PlatformIds!));
    }

    [Fact]
    public async Task GenerateGame_ForSeparateMappings_AssignsDifferentPlatforms()
    {
        var nes = new RomMPlatform { Id = 1, Name = "Nintendo Entertainment System" };
        var snes = new RomMPlatform { Id = 2, Name = "Super Nintendo" };
        var (nesImporter, _) = CreateImporter(nes);
        var (snesImporter, _) = CreateImporter(snes);

        var nesGame = await nesImporter.GenerateGame(CreateRom(42, 1));
        var snesGame = await snesImporter.GenerateGame(CreateRom(43, 2));

        Assert.NotNull(nesGame);
        Assert.NotNull(snesGame);
        Assert.Equal("nintendo entertainment system", Assert.Single(nesGame.PlatformIds!));
        Assert.Equal("super nintendo", Assert.Single(snesGame.PlatformIds!));
    }

    [Fact]
    public async Task GenerateGame_WhenPlatformNameContainsPunctuation_PreservesItsPlatformId()
    {
        var (importer, _) = CreateImporter(new RomMPlatform { Id = 9, Name = "PC Engine / TurboGrafx-16" });

        var game = await importer.GenerateGame(CreateRom(42, 9));

        Assert.NotNull(game);
        Assert.Equal("pc engine / turbografx-16", Assert.Single(game.PlatformIds!));
    }

    [Fact]
    public async Task GenerateGame_WithPlatformHavingNoRomCount_DoesNotRejectMappedGame()
    {
        var (importer, _) = CreateImporter(new RomMPlatform { Id = 50, Name = "Wonderswan", RomCount = 0 });

        var game = await importer.GenerateGame(CreateRom(42, 50));

        Assert.NotNull(game);
        Assert.Equal("wonderswan", Assert.Single(game.PlatformIds!));
    }

    private static (GravitonImport Importer, PlayniteFake Playnite) CreateImporter(RomMPlatform platform)
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var mapping = new EmulatorMapping
        {
            MappingId = Guid.NewGuid(),
            RomMPlatform = platform,
            DestinationPath = "C:/Games"
        };
        var importer = new GravitonImport(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            args: new ImportGamesArgs(),
            mapping: mapping);

        return (importer, playnite);
    }

    private static RomMRom CreateRom(int id, int platformId)
    {
        return new RomMRom
        {
            Id = id,
            Name = "Platform Example",
            FileName = "platform.rom",
            FileNameNoExt = "platform",
            HasMultipleFiles = true,
            PlatformId = platformId
        };
    }
}
