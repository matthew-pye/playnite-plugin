using Graviton.Install.Downloads;
using Graviton.Models.ROM;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;

using NSubstitute;

using Playnite;

using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Install;

[Collection("GravitonPlayLifecycle")]
public sealed class GravitonUninstallControllerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GravitonUninstallTests", Guid.NewGuid().ToString("N"));
    private readonly GravitonContextFake _plugin = new();
    private readonly PlayniteFake _playnite = new();
    private readonly IPlayniteApi _previousLocalizationApi;

    public GravitonUninstallControllerTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Games"));
        _plugin.ConfigurePluginDataDirectory(_root);
        _previousLocalizationApi = Loc.Api;
        Loc.Api = _playnite.Api;
    }

    [Fact]
    public async Task UninstallAsync_ForSingleFile_RemovesOnlyTrackedFileAndPersistsRemoval()
    {
        var installed = WriteFile("Installed/game.rom", [1, 2, 3]);
        var save = WriteFile("Installed/save.sav", [9, 8, 7]);
        var (controller, _, rom) = Target(installed);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.False(File.Exists(installed));
        Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(save));
        Assert.True(Directory.Exists(Path.GetDirectoryName(installed)));
        Assert.Null(rom.InstalledPath);
        Assert.False(StoredROM(42).IsInstalledPathDirectory);
        Assert.Null(StoredROM(42).InstalledPath);
    }

    [Fact]
    public async Task UninstallAsync_ForDirectory_RemovesItsContentsButNotSiblingDirectory()
    {
        var installed = Path.Combine(_root, "Installed", "Game");
        WriteFile("Installed/Game/Inner/game.rom", [1]);
        var separate = WriteFile("Installed/Other/save.sav", [3, 4]);
        var (controller, _, rom) = Target(installed, isDirectory: true);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.False(Directory.Exists(installed));
        Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(separate));
        Assert.Null(rom.InstalledPath);
        Assert.False(rom.IsInstalledPathDirectory);
        Assert.Null(StoredROM(42).InstalledPath);
        Assert.False(StoredROM(42).IsInstalledPathDirectory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UninstallAsync_WhenTrackedPathNoLongerExists_ClearsSavedInstallState(bool isDirectory)
    {
        var (controller, _, rom) = Target(Path.Combine(_root, "Missing", "game"), isDirectory);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.Null(rom.InstalledPath);
        Assert.False(rom.IsInstalledPathDirectory);
        Assert.Null(StoredROM(42).InstalledPath);
        Assert.False(StoredROM(42).IsInstalledPathDirectory);
        await _playnite.Dialogs.Api.Received(1).ShowErrorMessageAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Exception?>());
    }

    [Fact]
    public async Task UninstallAsync_WhenDirectoryFlagDoesNotMatchFile_PreservesTheFile()
    {
        var installed = WriteFile("Installed/game.rom", [1, 2, 3]);
        var (controller, _, _) = Target(installed, isDirectory: true);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(installed));
        Assert.Null(StoredROM(42).InstalledPath);
        Assert.False(StoredROM(42).IsInstalledPathDirectory);
    }

    [Fact]
    public async Task UninstallAsync_WhenFileFlagDoesNotMatchDirectory_PreservesTheDirectory()
    {
        var installed = Path.Combine(_root, "Installed", "Game");
        var file = WriteFile("Installed/Game/game.rom", [1, 2, 3]);
        var (controller, _, _) = Target(installed, isDirectory: false);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.True(Directory.Exists(installed));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(file));
        Assert.Null(StoredROM(42).InstalledPath);
    }

    [Fact]
    public async Task UninstallAsync_WhenTrackedGameHasNoInstallPath_DoesNotDeleteUnrelatedFiles()
    {
        var unrelated = WriteFile("Installed/other.rom", [5, 6]);
        var (controller, _, _) = Target(null);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.Equal(new byte[] { 5, 6 }, File.ReadAllBytes(unrelated));
        Assert.False(File.Exists(Path.Combine(_root, "Games", "42.json")));
    }

    [Fact]
    public async Task UninstallAsync_WhenNoRomIsTracked_DoesNotDeleteUnrelatedFiles()
    {
        var unrelated = WriteFile("Installed/other.rom", [5, 6]);
        var game = new Game("Untracked Game") { LibraryGameId = "42", InstallState = InstallState.Installed };
        _playnite.Games.Seed(game);
        var controller = new GravitonUninstallController(_plugin.Context, _playnite.Api, new GravitonLogger(_plugin.Settings), game);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.Equal(new byte[] { 5, 6 }, File.ReadAllBytes(unrelated));
        Assert.False(File.Exists(Path.Combine(_root, "Games", "42.json")));
    }

    [Fact]
    public async Task UninstallAsync_WhenSharedDirectoryIsCancelled_PreservesBothGamesAndFiles()
    {
        var installed = Path.Combine(_root, "Shared");
        var gameFile = WriteFile("Shared/game.rom", [1, 2]);
        var (controller, _, rom) = Target(installed, isDirectory: true);
        var (otherGame, otherROM) = AddOtherGame(installed);
        _playnite.Dialogs.EnqueueMessageResponse(MessageBoxResult.Cancel);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(gameFile));
        Assert.Equal(installed, rom.InstalledPath);
        Assert.Equal(installed, otherROM.InstalledPath);
        Assert.Equal(InstallState.Installed, _playnite.Games.Api.Get(otherGame.Id)!.InstallState);
        Assert.False(File.Exists(Path.Combine(_root, "Games", "42.json")));
        Assert.False(File.Exists(Path.Combine(_root, "Games", "43.json")));
    }

    [Fact]
    public async Task UninstallAsync_WhenSharedDirectoryRemovalIsAccepted_UninstallsAndPersistsBothGames()
    {
        var installed = Path.Combine(_root, "Shared");
        WriteFile("Shared/game.rom", [1, 2]);
        var (controller, _, rom) = Target(installed, isDirectory: true);
        var (otherGame, otherROM) = AddOtherGame(installed);
        _playnite.Dialogs.EnqueueMessageResponse(MessageBoxResult.Yes);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.False(Directory.Exists(installed));
        Assert.Null(rom.InstalledPath);
        Assert.Null(otherROM.InstalledPath);
        Assert.False(otherROM.IsInstalledPathDirectory);
        Assert.Null(StoredROM(42).InstalledPath);
        Assert.Null(StoredROM(43).InstalledPath);
        Assert.Equal(InstallState.Uninstalled, _playnite.Games.Api.Get(otherGame.Id)!.InstallState);
    }

    [Fact]
    public async Task UninstallAsync_WhenSharedDirectoryRemovalIsDeclined_KeepsOtherGameMarkedInstalled()
    {
        var installed = Path.Combine(_root, "Shared");
        WriteFile("Shared/game.rom", [1, 2]);
        var (controller, _, _) = Target(installed, isDirectory: true);
        var (otherGame, otherROM) = AddOtherGame(installed);
        _playnite.Dialogs.EnqueueMessageResponse(MessageBoxResult.No);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.False(Directory.Exists(installed));
        Assert.Null(StoredROM(42).InstalledPath);
        Assert.Equal(installed, otherROM.InstalledPath);
        Assert.Equal(InstallState.Installed, _playnite.Games.Api.Get(otherGame.Id)!.InstallState);
        Assert.False(File.Exists(Path.Combine(_root, "Games", "43.json")));
    }

    [Fact]
    public async Task UninstallAsync_WhenDifferentGameHasDifferentInstallFolder_DoesNotTreatItAsCollision()
    {
        var target = Path.Combine(_root, "Installed", "First");
        WriteFile("Installed/First/game.rom", [1]);
        var separate = Path.Combine(_root, "Installed", "Second");
        var file = WriteFile("Installed/Second/game.rom", [2]);
        var (controller, _, _) = Target(target, isDirectory: true);
        var (otherGame, otherROM) = AddOtherGame(separate);

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.False(Directory.Exists(target));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(file));
        Assert.Equal(separate, otherROM.InstalledPath);
        Assert.Equal(InstallState.Installed, _playnite.Games.Api.Get(otherGame.Id)!.InstallState);
    }

    [Fact]
    public async Task UninstallAsync_WhenSharedPathHasSamePlayniteId_DoesNotPromptForCollision()
    {
        var installed = Path.Combine(_root, "Shared");
        WriteFile("Shared/game.rom", [1]);
        var (controller, game, _) = Target(installed, isDirectory: true);
        _plugin.ImportedGames["43"] = new RomMRomLocal
        {
            Id = 43,
            PlayniteID = game.Id,
            InstalledPath = installed,
            IsInstalledPathDirectory = true
        };

        await controller.UninstallAsync(new UninstallController.UninstallActionArgs());

        Assert.False(Directory.Exists(installed));
        Assert.Null(StoredROM(42).InstalledPath);
    }

    private (GravitonUninstallController Controller, Game Game, RomMRomLocal Rom) Target(string? installedPath, bool isDirectory = false)
    {
        var game = new Game("Example Game")
        {
            LibraryGameId = "42",
            InstallState = InstallState.Installed
        };
        _playnite.Games.Seed(game);
        var rom = new RomMRomLocal
        {
            Id = 42,
            PlayniteID = game.Id,
            InstalledPath = installedPath,
            IsInstalledPathDirectory = isDirectory
        };
        _plugin.ImportedGames["42"] = rom;
        return (new GravitonUninstallController(_plugin.Context, _playnite.Api, new GravitonLogger(_plugin.Settings), game), game, rom);
    }

    private (Game Game, RomMRomLocal Rom) AddOtherGame(string installedPath)
    {
        var game = new Game("Another Game")
        {
            LibraryGameId = "43",
            InstallState = InstallState.Installed
        };
        _playnite.Games.Seed(game);
        var rom = new RomMRomLocal
        {
            Id = 43,
            PlayniteID = game.Id,
            InstalledPath = installedPath,
            IsInstalledPathDirectory = true
        };
        _plugin.ImportedGames["43"] = rom;
        return (game, rom);
    }

    private RomMRomLocal StoredROM(int id)
    {
        var json = File.ReadAllText(Path.Combine(_root, "Games", $"{id}.json"));
        return JsonSerializer.Deserialize<RomMRomLocal>(json)!;
    }

    private string WriteFile(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public void Dispose()
    {
        Loc.Api = _previousLocalizationApi;
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
