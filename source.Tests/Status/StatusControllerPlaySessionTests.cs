using Graviton.Models.RomM.PlaySessions;
using Graviton.Notifications;
using Graviton.Status;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Status;

public sealed class StatusControllerPlaySessionTests
{
    private const string BasePath = "/api/play-sessions?limit=50";

    [Fact]
    public async Task FetchPlaySessions_WhenServerReturnsEmptyList_ReturnsEmptyList()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=0", "[]");

        var sessions = await controller.FetchPlaySessions();

        Assert.NotNull(sessions);
        Assert.Empty(sessions);
        await romm.Server.Received(1).GETAsync($"{BasePath}&offset=0", false);
    }

    [Fact]
    public async Task FetchPlaySessions_WhenPageIsNotFull_StopsAfterFirstPage()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=0", CreateSessionPage(1, 2));

        var sessions = await controller.FetchPlaySessions();

        Assert.NotNull(sessions);
        Assert.Equal(new[] { 1, 2 }, sessions.Select(session => session.ID));
        await romm.Server.DidNotReceive().GETAsync($"{BasePath}&offset=50", false);
    }

    [Fact]
    public async Task FetchPlaySessions_WhenPageIsFull_LoadsTheNextPage()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=0", CreateSessionPage(1, 50));
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=50", CreateSessionPage(51, 2));

        var sessions = await controller.FetchPlaySessions();

        Assert.NotNull(sessions);
        Assert.Equal(52, sessions.Count);
        Assert.Equal(1, sessions[0].ID);
        Assert.Equal(52, sessions[^1].ID);
        await romm.Server.Received(1).GETAsync($"{BasePath}&offset=50", false);
    }

    [Fact]
    public async Task FetchPlaySessions_WhenNextPageIsEmpty_StopsWithoutExtraRequests()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=0", CreateSessionPage(1, 50));
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=50", "[]");

        var sessions = await controller.FetchPlaySessions();

        Assert.NotNull(sessions);
        Assert.Equal(50, sessions.Count);
        await romm.Server.DidNotReceive().GETAsync($"{BasePath}&offset=100", false);
    }

    [Fact]
    public async Task FetchPlaySessions_WhenFirstRequestReturnsNull_ReturnsNull()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=0", null);

        var sessions = await controller.FetchPlaySessions();

        Assert.Null(sessions);
    }

    [Fact]
    public async Task FetchPlaySessions_WhenLaterRequestReturnsNull_DoesNotReturnIncompleteData()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=0", CreateSessionPage(1, 50));
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=50", null);

        var sessions = await controller.FetchPlaySessions();

        Assert.Null(sessions);
    }

    [Fact]
    public async Task FetchPlaySessions_WithRomId_FiltersTheServerRequest()
    {
        var (controller, _, romm) = CreateController();
        const string path = "/api/play-sessions?limit=50&rom_id=42&offset=0";
        romm.ConfigureJsonGETResponse(path, CreateSessionPage(1, 1));

        var sessions = await controller.FetchPlaySessions(romID: 42);

        Assert.Single(Assert.IsType<List<RomMPlaySession>>(sessions));
        await romm.Server.Received(1).GETAsync(path, false);
    }

    [Fact]
    public async Task FetchPlaySessions_WhenLimitedToCurrentDevice_AddsDeviceFilter()
    {
        var (controller, plugin, romm) = CreateController();
        plugin.Settings.ImportPlaysessions = ImportPlaySessions.OnlyThisDevice;
        plugin.Settings.AccountState.DeviceID = "handheld-01";
        const string path = "/api/play-sessions?limit=50&device_id=handheld-01&offset=0";
        romm.ConfigureJsonGETResponse(path, "[]");

        var sessions = await controller.FetchPlaySessions();

        Assert.Empty(Assert.IsType<List<RomMPlaySession>>(sessions));
        await romm.Server.Received(1).GETAsync(path, false);
    }

    [Fact]
    public async Task FetchPlaySessions_WhenImportingAllDevices_DoesNotAddDeviceFilter()
    {
        var (controller, plugin, romm) = CreateController();
        plugin.Settings.ImportPlaysessions = ImportPlaySessions.All;
        plugin.Settings.AccountState.DeviceID = "handheld-01";
        romm.ConfigureJsonGETResponse($"{BasePath}&offset=0", "[]");

        await controller.FetchPlaySessions();

        await romm.Server.Received(1).GETAsync($"{BasePath}&offset=0", false);
    }

    private static (StatusController Controller, GravitonContextFake Plugin, RomMServerFake RomM) CreateController()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();

        var controller = new StatusController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            romMServer: romm.Server);

        return (controller, plugin, romm);
    }

    private static string CreateSessionPage(int firstId, int count)
    {
        var sessions = Enumerable.Range(firstId, count).Select(id => new
        {
            id,
            user_id = 7,
            device_id = "handheld-01",
            rom_id = 42,
            save_slot = (string?)null,
            start_time = "2026-10-10T10:00:00Z",
            end_time = "2026-10-10T10:01:00Z",
            duration_ms = 60000,
            created_at = "2026-10-10T10:01:00Z",
            updated_at = "2026-10-10T10:01:00Z"
        });

        return JsonSerializer.Serialize(sessions);
    }
}
