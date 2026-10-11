using Graviton.Models.RomM;
using Graviton.Tests.Fakes.Settings;

using NSubstitute;

using Xunit;

namespace Graviton.Tests.Settings;

[Collection("RomMAuthentication")]
public sealed class RomMAuthenticationDeviceTests
{
    [Fact]
    public async Task RegisterNewDevice_WhenCurrentDeviceExists_ReusesIt()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.AccountState.DeviceID = "device-123";
        test.RomM.ConfigureJsonGETResponse("/api/devices", """
            [{"id":"device-123","user_id":1,"name":"Graviton","platform":"Windows"}]
            """);

        var result = await test.Authentication.RegisterNewDevice();

        Assert.True(result);
        Assert.Equal("device-123", test.Plugin.Settings.AccountState.DeviceID);
        await test.RomM.Server.Received(1).GETAsync("/api/devices", false);
        await test.RomM.Server.DidNotReceive().POSTAsync("/api/devices", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task RegisterNewDevice_WhenCurrentDeviceIsAbsent_RegistersAnother()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.AccountState.DeviceID = "old-device";
        test.RomM.ConfigureJsonGETResponse("/api/devices", """
            [{"id":"other-device","user_id":1,"name":"Other","platform":"Windows"}]
            """);
        test.ConfigureDeviceRegistration("replacement-device");

        var result = await test.Authentication.RegisterNewDevice();

        Assert.True(result);
        Assert.Equal("replacement-device", test.Plugin.Settings.AccountState.DeviceID);
        await test.RomM.Server.Received(1).POSTAsync("/api/devices", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task RegisterNewDevice_WithNoStoredDevice_DoesNotFetchDeviceList()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureDeviceRegistration("first-device");

        var result = await test.Authentication.RegisterNewDevice();

        Assert.True(result);
        Assert.Equal("first-device", test.Plugin.Settings.AccountState.DeviceID);
        await test.RomM.Server.DidNotReceive().GETAsync("/api/devices", Arg.Any<bool>());
    }

    [Fact]
    public async Task RegisterNewDevice_WhenDeviceListRequestFails_RegistersNewDevice()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.AccountState.DeviceID = "old-device";
        test.RomM.ConfigureJsonGETResponse("/api/devices", null);
        test.ConfigureDeviceRegistration("new-device");

        var result = await test.Authentication.RegisterNewDevice();

        Assert.True(result);
        Assert.Equal("new-device", test.Plugin.Settings.AccountState.DeviceID);
    }

    [Fact]
    public async Task RegisterNewDevice_WhenCreateFails_DoesNotReplaceStoredDevice()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonPOSTResponse("/api/devices", null);

        var result = await test.Authentication.RegisterNewDevice();

        Assert.False(result);
        Assert.Equal("", test.Plugin.Settings.AccountState.DeviceID);
    }

    [Fact]
    public async Task RegisterNewDevice_WhenResponseOmitsDeviceID_ReportsFailure()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonPOSTResponse("/api/devices", """
            {"name":"Graviton","created_at":"2026-10-10T10:00:00Z"}
            """);

        var result = await test.Authentication.RegisterNewDevice();

        Assert.False(result);
        Assert.Equal("", test.Plugin.Settings.AccountState.DeviceID);
    }

    [Fact]
    public async Task RegisterNewDevice_WhenDeviceListCannotDeserialize_DoesNotRegister()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.AccountState.DeviceID = "previous-device";
        test.RomM.ConfigureJsonGETResponse("/api/devices", "{}");

        var result = await test.Authentication.RegisterNewDevice();

        Assert.False(result);
        Assert.Equal("previous-device", test.Plugin.Settings.AccountState.DeviceID);
        await test.RomM.Server.DidNotReceive().POSTAsync("/api/devices", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task RegisterNewDevice_WithToken_EnablesRemoteInstallCapability()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureToken();
        test.ConfigureDeviceRegistration();

        var result = await test.Authentication.RegisterNewDevice();

        Assert.True(result);
        await test.RomM.Server.Received(1).POSTAsync(
            "/api/devices",
            Arg.Is<object>(value => value is RomMRegisterDevice && ((RomMRegisterDevice)value).Capabilities.RemoteInstall),
            false);
    }

    [Fact]
    public async Task RegisterNewDevice_WithBasicAuth_DisablesRemoteInstallCapability()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureBasicAuth();
        test.ConfigureDeviceRegistration();

        var result = await test.Authentication.RegisterNewDevice();

        Assert.True(result);
        await test.RomM.Server.Received(1).POSTAsync(
            "/api/devices",
            Arg.Is<object>(value => value is RomMRegisterDevice && !((RomMRegisterDevice)value).Capabilities.RemoteInstall),
            false);
    }

    [Fact]
    public async Task RegisterNewDevice_RequestContainsClientAndMachineMetadata()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureDeviceRegistration();

        var result = await test.Authentication.RegisterNewDevice();

        Assert.True(result);
        await test.RomM.Server.Received(1).POSTAsync(
            "/api/devices",
            Arg.Is<object>(value => value is RomMRegisterDevice &&
                ((RomMRegisterDevice)value).Platform == "Windows" &&
                ((RomMRegisterDevice)value).Client == "Graviton" &&
                ((RomMRegisterDevice)value).HostName == Environment.MachineName &&
                ((RomMRegisterDevice)value).ClientVersion == GravitonPlugin.Version.ToString()),
            false);
    }

    [Fact]
    public async Task UpdateDevice_WhenNoDeviceID_DoesNotSendRequest()
    {
        using var test = new AuthenticationTestHarness();

        var result = await test.Authentication.UpdateDevice();

        Assert.False(result);
        await test.RomM.Server.DidNotReceive().PUTAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task UpdateDevice_WhenRequestFails_ReturnsFalse()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.AccountState.DeviceID = "device-123";
        test.RomM.ConfigureJsonPUTResponse("/api/devices/device-123", null);

        var result = await test.Authentication.UpdateDevice();

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateDevice_WhenServerAcceptsUpdate_ReturnsTrue()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.AccountState.DeviceID = "device-123";
        test.RomM.ConfigureJsonPUTResponse("/api/devices/device-123", "{}");

        var result = await test.Authentication.UpdateDevice();

        Assert.True(result);
        await test.RomM.Server.Received(1).PUTAsync("/api/devices/device-123", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task UpdateDevice_WithToken_SendsRemoteInstallCapability()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureToken();
        test.Plugin.Settings.AccountState.DeviceID = "device-123";
        test.RomM.ConfigureJsonPUTResponse("/api/devices/device-123", "{}");

        var result = await test.Authentication.UpdateDevice();

        Assert.True(result);
        await test.RomM.Server.Received(1).PUTAsync(
            "/api/devices/device-123",
            Arg.Is<object>(value => value is RomMRegisterDevice && ((RomMRegisterDevice)value).Capabilities.RemoteInstall),
            false);
    }

    [Fact]
    public async Task UpdateDevice_WithBasicAuth_DisablesRemoteInstallCapability()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureBasicAuth();
        test.Plugin.Settings.AccountState.DeviceID = "device-123";
        test.RomM.ConfigureJsonPUTResponse("/api/devices/device-123", "{}");

        var result = await test.Authentication.UpdateDevice();

        Assert.True(result);
        await test.RomM.Server.Received(1).PUTAsync(
            "/api/devices/device-123",
            Arg.Is<object>(value => value is RomMRegisterDevice && !((RomMRegisterDevice)value).Capabilities.RemoteInstall),
            false);
    }
}
