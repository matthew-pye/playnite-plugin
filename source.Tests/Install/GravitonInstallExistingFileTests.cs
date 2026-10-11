using Graviton.Install;
using Graviton.Models;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Rom;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;
using Graviton.Notifications;

using Playnite;

using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class GravitonInstallExistingFileTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "Graviton.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly GravitonContextFake _plugin = new();
    private readonly PlayniteFake _playnite = new();
    private readonly RomMServerFake _romm = new();

    public GravitonInstallExistingFileTests()
    {
        Directory.CreateDirectory(Path.Combine(_testDirectory, "Games"));
        _plugin.ConfigurePluginDataDirectory(_testDirectory);
    }

    [Fact]
    public async Task DownloadInstallROM_WhenFileExists_UpdatesPlayniteWithoutDownloading()
    {
        var installDirectory = Path.Combine(_testDirectory, "Installed");
        var setup = CreateGame(installDirectory);
        var controller = CreateController();

        await controller.DownloadInstallROM(setup.Info, setup.Game, new RomMRom { Id = 42 });

        var storedGame = _playnite.Games.Api.Get(setup.Game.Id);
        Assert.NotNull(storedGame);
        Assert.Equal(InstallState.Installed, storedGame.InstallState);
        Assert.Equal(setup.FilePath, setup.LocalROM.InstalledPath);
        Assert.False(setup.LocalROM.IsInstalledPathDirectory);
        Assert.True(File.Exists(Path.Combine(_testDirectory, "Games", "42.json")));
    }

    [Fact]
    public async Task DownloadInstallROM_WhenFileExists_InvokesCallbackWithoutUpdatingPlaynite()
    {
        var installDirectory = Path.Combine(_testDirectory, "Installed");
        var setup = CreateGame(installDirectory);
        var controller = CreateController();
        var callbackCalls = 0;
        string? callbackDirectory = null;
        ulong callbackSize = 0;

        await controller.DownloadInstallROM(setup.Info, setup.Game, new RomMRom { Id = 42 }, (directory, size) =>
        {
            callbackCalls++;
            callbackDirectory = directory;
            callbackSize = size;
            return Task.CompletedTask;
        });

        Assert.Equal(1, callbackCalls);
        Assert.Equal(installDirectory, callbackDirectory);
        Assert.Equal((ulong)new FileInfo(setup.FilePath).Length, callbackSize);
        Assert.Equal(InstallState.Uninstalled, _playnite.Games.Api.Get(setup.Game.Id)!.InstallState);
        Assert.Equal(setup.FilePath, setup.LocalROM.InstalledPath);

        var savedPath = Path.Combine(_testDirectory, "Games", "42.json");
        var savedRom = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText(savedPath));
        Assert.NotNull(savedRom);
        Assert.Equal(setup.FilePath, savedRom.InstalledPath);
    }

    [Fact]
    public async Task DownloadInstallROM_WhenFileExistsInSubdirectory_PersistsDirectoryInstallPath()
    {
        var destinationDirectory = Path.Combine(_testDirectory, "Installed");
        var subdirectory = Path.Combine(destinationDirectory, "Subfolder");
        var setup = CreateGame(destinationDirectory, subdirectory);
        var controller = CreateController();

        await controller.DownloadInstallROM(setup.Info, setup.Game, new RomMRom { Id = 42 });

        Assert.Equal(subdirectory, setup.LocalROM.InstalledPath);
        Assert.True(setup.LocalROM.IsInstalledPathDirectory);

        var savedPath = Path.Combine(_testDirectory, "Games", "42.json");
        var savedRom = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText(savedPath));
        Assert.NotNull(savedRom);
        Assert.Equal(subdirectory, savedRom.InstalledPath);
        Assert.True(savedRom.IsInstalledPathDirectory);
    }

    private GravitonInstallController CreateController()
    {
        return new GravitonInstallController(
            plugin: _plugin.Context,
            playniteAPI: _playnite.Api,
            logger: new GravitonLogger(),
            server: _romm.Server);
    }

    private (Game Game, RomMRomLocal LocalROM, GameInstallInfo Info, string FilePath) CreateGame(
        string destinationDirectory,
        string? installDirectory = null)
    {
        installDirectory ??= destinationDirectory;
        Directory.CreateDirectory(installDirectory);

        var filePath = Path.Combine(installDirectory, "example.rom");
        File.WriteAllBytes(filePath, [0x00, 0xFF, 0x42, 0x80]);

        var game = new Game("Example Game")
        {
            LibraryGameId = "42",
            InstallState = InstallState.Uninstalled
        };

        var localROM = new RomMRomLocal
        {
            Id = 42,
            PlayniteID = game.Id,
            Name = game.Name
        };

        var mapping = new EmulatorMapping
        {
            DestinationPath = destinationDirectory
        };

        var installInfo = new GameInstallInfo
        {
            Id = 42,
            FileName = "example.rom",
            InstallPath = installDirectory == destinationDirectory
                ? EmulatorMapping.InstallPathToken
                : Path.Combine(EmulatorMapping.InstallPathToken, "Subfolder"),
            Mapping = mapping,
            HasMultipleFiles = false
        };

        _plugin.ImportedGames["42"] = localROM;
        _playnite.Games.Seed(game);

        return (game, localROM, installInfo, filePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
