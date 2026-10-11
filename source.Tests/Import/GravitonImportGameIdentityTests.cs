using Graviton.Import;
using Graviton.Models;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;

using System.IO;

using Xunit;

using static Playnite.Plugin;

namespace Graviton.Tests.Import;

public sealed class GravitonImportGameIdentityTests
{
    [Theory]
    [InlineData("revision-a.z64", "revision-b.z64")]
    [InlineData("game (US).z64", "game (EU).z64")]
    [InlineData("game.z64", "game.zip")]
    public async Task GenerateGame_WhenFilenameChanges_KeepsRomMGameId(string originalFile, string updatedFile)
    {
        var (importer, _) = CreateImporter();
        var original = CreateRom(42, originalFile);
        var updated = CreateRom(42, updatedFile);

        var originalGame = await importer.GenerateGame(original);
        var updatedGame = await importer.GenerateGame(updated);

        Assert.NotNull(originalGame);
        Assert.NotNull(updatedGame);
        Assert.Equal("42", originalGame.LibraryGameId);
        Assert.Equal(originalGame.LibraryGameId, updatedGame.LibraryGameId);
    }

    [Fact]
    public async Task GenerateGame_WhenHashChanges_KeepsRomMGameId()
    {
        var (importer, _) = CreateImporter();
        var original = CreateRom(42, "game.z64");
        var updated = CreateRom(42, "game.z64");
        original.SHA1 = new string('a', 40);
        updated.SHA1 = new string('b', 40);

        var originalGame = await importer.GenerateGame(original);
        var updatedGame = await importer.GenerateGame(updated);

        Assert.NotNull(originalGame);
        Assert.NotNull(updatedGame);
        Assert.Equal("42", originalGame.LibraryGameId);
        Assert.Equal(originalGame.LibraryGameId, updatedGame.LibraryGameId);
    }

    [Fact]
    public async Task GenerateGame_WhenRomNameChanges_KeepsRomMGameId()
    {
        var (importer, _) = CreateImporter();
        var original = CreateRom(42, "game.z64");
        var updated = CreateRom(42, "game.z64");
        original.Name = "Original Name";
        updated.Name = "Updated Name";

        var originalGame = await importer.GenerateGame(original);
        var updatedGame = await importer.GenerateGame(updated);

        Assert.NotNull(originalGame);
        Assert.NotNull(updatedGame);
        Assert.Equal(originalGame.LibraryGameId, updatedGame.LibraryGameId);
        Assert.Equal("Updated Name", updatedGame.Name);
    }

    [Fact]
    public async Task GenerateGame_ForDifferentRomIds_UsesDistinctLibraryGameIds()
    {
        var (importer, _) = CreateImporter();
        var first = await importer.GenerateGame(CreateRom(42, "same-name.z64"));
        var second = await importer.GenerateGame(CreateRom(43, "same-name.z64"));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("42", first.LibraryGameId);
        Assert.Equal("43", second.LibraryGameId);
    }

    [Fact]
    public async Task GenerateGame_UsesGravitonLibraryAndSourceIds()
    {
        var (importer, _) = CreateImporter();

        var game = await importer.GenerateGame(CreateRom(42, "game.z64"));

        Assert.NotNull(game);
        Assert.Equal(GravitonPlugin.Id, game.LibraryId);
        Assert.Equal(GravitonPlugin.Id, game.SourceId);
        Assert.Equal("42", game.LibraryGameId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GenerateGame_WithoutName_ReturnsNull(string? name)
    {
        var (importer, playnite) = CreateImporter();
        var rom = CreateRom(42, "game.z64");
        rom.Name = name;

        var game = await importer.GenerateGame(rom);

        Assert.Null(game);
        Assert.Empty(playnite.GameDescriptions.Api);
    }

    private static (GravitonImport Importer, PlayniteFake Playnite) CreateImporter()
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

        return (importer, playnite);
    }

    private static RomMRom CreateRom(int id, string filename)
    {
        return new RomMRom
        {
            Id = id,
            Name = "Example Game",
            FileName = filename,
            FileNameNoExt = Path.GetFileNameWithoutExtension(filename),
            HasMultipleFiles = true,
            PlatformId = 17,
            FileSizeBytes = 2048
        };
    }
}
