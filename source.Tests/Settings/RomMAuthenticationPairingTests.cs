using Graviton.Tests.Fakes.Settings;

using NSubstitute;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Settings;

[Collection("RomMAuthentication")]
public sealed class RomMAuthenticationPairingTests
{
    [Fact]
    public async Task InitDevicePair_UsesPublicEndpointAndDeserializesResponse()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonPOSTResponse("/api/auth/device/init", """
            {"device_code":"device-code","user_code":"ABCD-1234","verification_path":"/pair","verification_path_complete":"/pair?code=ABCD-1234","expires_in":600,"interval":5}
            """);

        var result = await test.Authentication.InitDevicePair();

        Assert.NotNull(result);
        Assert.Equal("device-code", result.DeviceCode);
        Assert.Equal("ABCD-1234", result.UserCode);
        Assert.Equal("/pair", result.VerificationPath);
        Assert.Equal(600, result.ExpiresIn);
        Assert.Equal(5, result.Interval);
        await test.RomM.Server.Received(1).POSTAsync("/api/auth/device/init", Arg.Any<object>(), true);
    }

    [Fact]
    public async Task InitDevicePair_RequestIncludesMachineAndClientMetadata()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonPOSTResponse("/api/auth/device/init", """
            {"device_code":"device-code","user_code":"ABCD-1234","expires_in":600,"interval":5}
            """);

        var result = await test.Authentication.InitDevicePair();

        Assert.NotNull(result);
        await test.RomM.Server.Received(1).POSTAsync(
            "/api/auth/device/init",
            Arg.Is<object>(value =>
                JsonSerializer.Serialize(value).Contains("Graviton-" + Environment.MachineName) &&
                JsonSerializer.Serialize(value).Contains("Graviton (Playnite Plugin)") &&
                JsonSerializer.Serialize(value).Contains("\"platform\":\"Windows\"") &&
                JsonSerializer.Serialize(value).Contains("\"client_version\":")),
            true);
    }

    [Fact]
    public async Task InitDevicePair_RequestsRequiredROMAndDeviceScopes()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonPOSTResponse("/api/auth/device/init", """
            {"device_code":"device-code","user_code":"ABCD-1234","expires_in":600,"interval":5}
            """);

        var result = await test.Authentication.InitDevicePair();

        Assert.NotNull(result);
        await test.RomM.Server.Received(1).POSTAsync(
            "/api/auth/device/init",
            Arg.Is<object>(value =>
                JsonSerializer.Serialize(value).Contains("me.read") &&
                JsonSerializer.Serialize(value).Contains("devices.write") &&
                JsonSerializer.Serialize(value).Contains("roms.user.write") &&
                JsonSerializer.Serialize(value).Contains("platforms.read")),
            true);
    }

    [Fact]
    public async Task InitDevicePair_WithoutHost_DoesNotContactServer()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.Host = "";

        var result = await test.Authentication.InitDevicePair();

        Assert.Null(result);
        await test.RomM.Server.DidNotReceive().POSTAsync("/api/auth/device/init", Arg.Any<object>(), true);
    }

    [Fact]
    public async Task InitDevicePair_WhenServerDoesNotRespond_ReturnsNull()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonPOSTResponse("/api/auth/device/init", null);

        var result = await test.Authentication.InitDevicePair();

        Assert.Null(result);
    }

    [Fact]
    public async Task InitDevicePair_WhenResponseHasWrongShape_ReturnsNull()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonPOSTResponse("/api/auth/device/init", "[]");

        var result = await test.Authentication.InitDevicePair();

        Assert.Null(result);
    }
}
