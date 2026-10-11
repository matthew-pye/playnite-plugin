using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveManagerTrackingTests
{
    [Theory]
    [InlineData(42, "handheld-device")]
    [InlineData(987, "desktop-device")]
    public async Task UntrackSave_SendsRomM540DeviceIDToCorrectSave(int saveId, string deviceId)
    {
        var (manager, plugin, romm) = CreateManager();
        plugin.Settings.AccountState.DeviceID = deviceId;
        romm.ConfigureJsonPOSTResponse($"/api/saves/{saveId}/untrack", CreateUntrackResponse(saveId));

        await manager.UntrackSave(saveId);

        await romm.Server.Received(1).POSTAsync(
            APIPath: $"/api/saves/{saveId}/untrack",
            json: Arg.Is<object>(body => HasDeviceID(body, deviceId)),
            PublicEndpoint: false);
    }

    [Fact]
    public async Task UntrackSave_CalledForTwoSaves_SendsTwoSeparateRequests()
    {
        var (manager, _, romm) = CreateManager();
        romm.ConfigureJsonPOSTResponse("/api/saves/11/untrack", CreateUntrackResponse(11));
        romm.ConfigureJsonPOSTResponse("/api/saves/22/untrack", CreateUntrackResponse(22));

        await manager.UntrackSave(11);
        await manager.UntrackSave(22);

        await romm.Server.Received(1).POSTAsync(
            APIPath: "/api/saves/11/untrack",
            json: Arg.Any<object>(),
            PublicEndpoint: false);
        await romm.Server.Received(1).POSTAsync(
            APIPath: "/api/saves/22/untrack",
            json: Arg.Any<object>(),
            PublicEndpoint: false);
    }

    private static string CreateUntrackResponse(int saveId)
    {
        var response = RomMSaveFixtures.CreateResponse(
            RomMSaveFixtures.CreateSave(saveId, 42, "autosave"));

        using var json = JsonDocument.Parse(response);
        return json.RootElement[0].GetRawText();
    }

    private static bool HasDeviceID(object body, string expected)
    {
        var json = JsonSerializer.SerializeToElement(body);
        return json.GetProperty("device_id").GetString() == expected;
    }

    private static (SaveManager Manager, GravitonContextFake Plugin, RomMServerFake RomM) CreateManager()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var logger = new GravitonLogger(plugin.Settings);
        plugin.Settings.AccountState.DeviceID = "handheld-device";

        var manager = new SaveManager(plugin.Context, playnite.Api, logger, romm.Server);
        return (manager, plugin, romm);
    }
}
