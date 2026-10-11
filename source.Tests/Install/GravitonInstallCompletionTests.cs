using Graviton.Install;
using Graviton.Models.ROM;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using Playnite;

using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class GravitonInstallCompletionTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "Graviton.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly GravitonContextFake _plugin = new();
    private readonly PlayniteFake _playnite = new();
    private readonly RomMServerFake _romm = new();

    public GravitonInstallCompletionTests()
    {
        Directory.CreateDirectory(Path.Combine(_testDirectory, "Games"));
        _plugin.ConfigurePluginDataDirectory(_testDirectory);
    }

    [Fact]
    public async Task CompleteBaseGameInstallationAsync_WithCallback_PersistsMetadataAndInvokesCallback()
    {
        var controller = CreateController();
        var game = CreateGame();
        var localROM = CreateLocalROM(game);
        var installDirectory = Path.Combine(_testDirectory, "Installed");
        var installedPath = Path.Combine(installDirectory, "example.rom");
        var callbackCount = 0;
        string? callbackDirectory = null;
        ulong callbackSize = 0;

        localROM.InstalledPath = installedPath;
        localROM.IsInstalledPathDirectory = false;

        await controller.CompleteBaseGameInstallationAsync(localROM, game, installDirectory, 4096, (directory, size) =>
        {
            callbackCount++;
            callbackDirectory = directory;
            callbackSize = size;

            return Task.CompletedTask;
        });

        var savedROM = ReadSavedROM();

        Assert.Equal(1, callbackCount);
        Assert.Equal(installDirectory, callbackDirectory);
        Assert.Equal((ulong)4096, callbackSize);
        Assert.Equal(installedPath, savedROM.InstalledPath);
        Assert.False(savedROM.IsInstalledPathDirectory);
        Assert.Equal(InstallState.Uninstalled, _playnite.Games.Api.Get(game.Id)!.InstallState);
    }

    [Fact]
    public async Task CompleteBaseGameInstallationAsync_WithoutCallback_PersistsMetadataAndUpdatesPlaynite()
    {
        var controller = CreateController();
        var game = CreateGame();
        var localROM = CreateLocalROM(game);
        var installDirectory = Path.Combine(_testDirectory, "Installed");

        localROM.InstalledPath = installDirectory;
        localROM.IsInstalledPathDirectory = true;

        await controller.CompleteBaseGameInstallationAsync(localROM, game, installDirectory, 8192);

        var savedROM = ReadSavedROM();
        var storedGame = _playnite.Games.Api.Get(game.Id);

        Assert.NotNull(storedGame);
        Assert.Equal(InstallState.Installed, storedGame.InstallState);
        Assert.Equal(installDirectory, storedGame.InstallDirectory);
        Assert.Equal((ulong)8192, storedGame.InstallSize);
        Assert.Equal(installDirectory, savedROM.InstalledPath);
        Assert.True(savedROM.IsInstalledPathDirectory);
    }

    private GravitonInstallController CreateController()
    {
        return new GravitonInstallController(
            plugin: _plugin.Context,
            playniteAPI: _playnite.Api,
            logger: new GravitonLogger(),
            server: _romm.Server);
    }

    private Game CreateGame()
    {
        var game = new Game("Example Game")
        {
            LibraryGameId = "42",
            InstallState = InstallState.Uninstalled
        };

        _playnite.Games.Seed(game);
        return game;
    }

    private RomMRomLocal CreateLocalROM(Game game)
    {
        var localROM = new RomMRomLocal
        {
            Id = 42,
            PlayniteID = game.Id,
            Name = game.Name
        };

        _plugin.ImportedGames["42"] = localROM;
        return localROM;
    }

    private RomMRomLocal ReadSavedROM()
    {
        var filePath = Path.Combine(_testDirectory, "Games", "42.json");
        Assert.True(File.Exists(filePath));

        var savedROM = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText(filePath));
        return Assert.IsType<RomMRomLocal>(savedROM);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
