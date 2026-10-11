using Graviton.Notifications;
using Graviton.Status;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using Playnite;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Status;

public sealed class StatusControllerCompletionStatusTests
{
    [Theory]
    [InlineData("Never Playing", false, false, "never_playing")]
    [InlineData("Abandoned", false, false, "retired")]
    [InlineData("On Hold", false, false, "incomplete")]
    [InlineData("Played", false, false, "finished")]
    [InlineData("Completed", false, false, "completed_100")]
    [InlineData("Plan to Play", true, false, null)]
    [InlineData("Playing", false, true, null)]
    [InlineData("Not Played", false, false, null)]
    public async Task UpdateStatus_MapsPlayniteCompletionStatusToRomMProperties(
        string statusName,
        bool backlogged,
        bool nowPlaying,
        string? remoteStatus)
    {
        var (controller, playnite, romm) = CreateController();
        var completionStatus = new CompletionStatus(statusName);
        playnite.CompletionStatuses.Seed(completionStatus);

        var game = new Game("Example Game")
        {
            LibraryGameId = "42",
            CompletionStatusId = completionStatus.Id
        };
        romm.ConfigureJsonPUTResponse("/api/roms/42/props");

        await controller.UpdateStatus(game);

        await romm.Server.Received(1).PUTAsync(
            APIPath: "/api/roms/42/props",
            json: Arg.Is<object>(value => MatchesProperties(value, backlogged, nowPlaying, remoteStatus)),
            PublicEndpoint: false);
    }

    [Fact]
    public async Task PushPlaySession_SendsDeviceAndDurationInRomMFormat()
    {
        var (controller, _, romm, plugin) = CreateControllerWithPlugin();
        plugin.Settings.AccountState.DeviceID = "handheld-01";
        romm.ConfigureJsonPOSTResponse("/api/play-sessions", """{"results":[{"index":0,"status":"created","id":1,"detail":null}],"created_count":1,"skipped_count":0}""");
        var stopTime = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        await controller.PushPlaySession("42", stopTime, 60000);

        await romm.Server.Received(1).POSTAsync(
            APIPath: "/api/play-sessions",
            json: Arg.Is<object>(value => MatchesSession(value, stopTime, 60000)),
            PublicEndpoint: false);
    }

    private static bool MatchesProperties(object value, bool backlogged, bool nowPlaying, string? status)
    {
        var data = JsonSerializer.SerializeToElement(value);
        var actualStatus = data.GetProperty("status");

        return data.GetProperty("backlogged").GetBoolean() == backlogged
            && data.GetProperty("now_playing").GetBoolean() == nowPlaying
            && (status is null
                ? actualStatus.ValueKind == JsonValueKind.Null
                : actualStatus.GetString() == status);
    }

    private static bool MatchesSession(object value, DateTime stopTime, uint duration)
    {
        var data = JsonSerializer.SerializeToElement(value);
        var sessions = data.GetProperty("sessions");

        if (sessions.GetArrayLength() != 1)
        {
            return false;
        }

        var session = sessions[0];

        return data.GetProperty("device_id").GetString() == "handheld-01"
            && session.GetProperty("rom_id").GetInt32() == 42
            && session.GetProperty("duration_ms").GetInt32() == duration
            && session.GetProperty("start_time").GetString() == stopTime.AddMilliseconds(-duration).ToString("O")
            && session.GetProperty("end_time").GetString() == stopTime.ToString("O");
    }

    private static (StatusController Controller, PlayniteFake Playnite, RomMServerFake RomM) CreateController()
    {
        var (controller, playnite, romm, _) = CreateControllerWithPlugin();
        return (controller, playnite, romm);
    }

    private static (StatusController Controller, PlayniteFake Playnite, RomMServerFake RomM, GravitonContextFake Plugin) CreateControllerWithPlugin()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();

        var controller = new StatusController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            romMServer: romm.Server);

        return (controller, playnite, romm, plugin);
    }
}
