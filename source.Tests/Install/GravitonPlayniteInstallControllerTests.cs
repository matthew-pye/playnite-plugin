using Graviton.Install;
using Graviton.Models.ROM;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using Playnite;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class GravitonPlayniteInstallControllerTests
{
    [Fact]
    public async Task InstallAsync_WhenCancelled_DoesNotRequestRomAndMarksGameUninstalled()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var game = new Game("Example Game")
        {
            LibraryGameId = "42",
            InstallState = InstallState.Installed
        };
        var otherGame = new Game("Another Game")
        {
            LibraryGameId = "43",
            InstallState = InstallState.Installed
        };

        playnite.Games.Seed(game, otherGame);

        var controller = new GravitonPlayniteInstallController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(),
            server: romm.Server,
            game: game,
            gameData: new GameInstallInfo
            {
                Id = (int)InstallStatus.Cancelled
            });

        await controller.InstallAsync(new InstallController.InstallActionArgs());

        var storedGame = playnite.Games.Api.Get(game.Id);
        var storedOtherGame = playnite.Games.Api.Get(otherGame.Id);

        Assert.NotNull(storedGame);
        Assert.NotNull(storedOtherGame);
        Assert.Equal(InstallState.Uninstalled, storedGame.InstallState);
        Assert.Equal(InstallState.Installed, storedOtherGame.InstallState);

        await romm.Server.DidNotReceive().GETAsync(
            Arg.Any<string>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task InstallAsync_WhenRomMReturnsNull_MarksGameUninstalled()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var game = new Game("Example Game")
        {
            LibraryGameId = "42",
            InstallState = InstallState.Installed
        };

        playnite.Games.Seed(game);
        romm.ConfigureJsonGETResponse("/api/roms/42", null);

        var controller = new GravitonPlayniteInstallController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(),
            server: romm.Server,
            game: game,
            gameData: new GameInstallInfo
            {
                Id = 42
            });

        await controller.InstallAsync(new InstallController.InstallActionArgs());

        var storedGame = playnite.Games.Api.Get(game.Id);

        Assert.NotNull(storedGame);
        Assert.Equal(InstallState.Uninstalled, storedGame.InstallState);

        await romm.Server.Received(1).GETAsync(
            "/api/roms/42",
            Arg.Any<bool>());
    }
}
