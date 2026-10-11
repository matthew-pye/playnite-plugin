using Graviton.Models;
using Graviton.Models.RomM.Platform;
using Graviton.Tests.Fakes.Settings;

using NSubstitute;

using System.Net;

using Xunit;

namespace Graviton.Tests.Settings;

[Collection("RomMAuthentication")]
public sealed class RomMAuthenticationLoginTests
{
    [Fact]
    public void ResetLocalAccountState_ClearsIdentityAndAuthenticationResults()
    {
        using var test = new AuthenticationTestHarness();
        var account = test.Plugin.Settings.AccountState;
        account.User = "former-user";
        account.UserType = "admin";
        account.ServerVersion = "5.4.0";
        account.AuthenticateFailed = HttpStatusCode.Unauthorized;
        account.LastAuthenticated = DateTime.UtcNow;

        test.Authentication.ResetLocalAccountState();

        Assert.Equal("----", account.User);
        Assert.Equal("----", account.UserType);
        Assert.Equal("---", account.ServerVersion);
        Assert.Null(account.AuthenticateFailed);
        Assert.Null(account.LastAuthenticated);
        Assert.StartsWith(test.ProfileImagePath, test.Plugin.Settings.ProfilePath);
    }

    [Fact]
    public async Task Login_WithoutHost_StopsBeforeAuthentication()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.Host = "";
        test.ConfigureToken();
        test.Plugin.Settings.AccountState.User = "old-user";

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        Assert.Equal("----", test.Plugin.Settings.AccountState.User);
        await test.RomM.Server.DidNotReceive().GETAsync(Arg.Any<string>(), Arg.Any<bool>());
        test.RomM.Server.DidNotReceive().ConfigureClientToken(Arg.Any<string>());
    }

    [Fact]
    public async Task Login_WithBasicAuthAndNoUsername_StopsBeforeHeartbeat()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureBasicAuth(username: "", password: "password");

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        await test.RomM.Server.DidNotReceive().GETAsync(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Login_WithBasicAuthAndNoPassword_StopsBeforeHeartbeat()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureBasicAuth(username: "tester", password: "");

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        await test.RomM.Server.DidNotReceive().GETAsync(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Login_WithoutClientToken_StopsBeforeHeartbeat()
    {
        using var test = new AuthenticationTestHarness();

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        await test.RomM.Server.DidNotReceive().GETAsync(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Login_WithBasicAuth_ConfiguresCredentialsBeforeHeartbeat()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureBasicAuth("tester", "password");
        test.RomM.ConfigureJsonGETResponse("/api/heartbeat", null);

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        test.RomM.Server.Received(1).ConfigureBasicAuth("tester", "password");
        Assert.Null(test.Plugin.Settings.AccountState.LastAuthenticated);
    }

    [Fact]
    public async Task Login_WithClientToken_ConfiguresTokenBeforeHeartbeat()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureToken("my-token");
        test.RomM.ConfigureJsonGETResponse("/api/heartbeat", null);

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        test.RomM.Server.Received(1).ConfigureClientToken("my-token");
        Assert.Null(test.Plugin.Settings.AccountState.LastAuthenticated);
    }

    [Fact]
    public async Task Login_WhenHeartbeatIsInvalid_DoesNotRegisterDevice()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureToken();
        test.RomM.ConfigureJsonGETResponse("/api/heartbeat", "{}");

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        await test.RomM.Server.DidNotReceive().POSTAsync("/api/devices", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task Login_WhenDeviceRegistrationFails_ClearsAuthenticatedTimestamp()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureToken();
        test.ConfigureHeartbeat();
        test.RomM.ConfigureJsonPOSTResponse("/api/devices", null);

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        Assert.Equal("5.4.0", test.Plugin.Settings.AccountState.ServerVersion);
        Assert.Null(test.Plugin.Settings.AccountState.LastAuthenticated);
        await test.RomM.Server.DidNotReceive().GETAsync("/api/users/me", false);
    }

    [Fact]
    public async Task Login_WhenDeviceUpdateFails_DoesNotReadUserProfile()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureToken();
        test.ConfigureHeartbeat();
        test.ConfigureDeviceRegistration();
        test.RomM.ConfigureJsonPUTResponse("/api/devices/device-123", null);

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        Assert.Null(test.Plugin.Settings.AccountState.LastAuthenticated);
        Assert.Equal("device-123", test.Plugin.Settings.AccountState.DeviceID);
        await test.RomM.Server.DidNotReceive().GETAsync("/api/users/me", false);
    }

    [Fact]
    public async Task Login_WhenUserDataRequestFails_DoesNotFetchPlatforms()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureToken();
        test.ConfigureHeartbeat();
        test.ConfigureDeviceRegistration();
        test.RomM.ConfigureJsonPUTResponse("/api/devices/device-123", "{}");
        test.RomM.ConfigureJsonGETResponse("/api/users/me", null);

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        Assert.Null(test.Plugin.Settings.AccountState.LastAuthenticated);
        await test.RomM.Server.DidNotReceive().GETAsync("/api/platforms", false);
    }

    [Fact]
    public async Task Login_WithoutImportController_FailsPlatformSyncAfterProfileSync()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureBasicAuth();
        test.ConfigureHeartbeat();
        test.ConfigureDeviceRegistration();
        test.RomM.ConfigureJsonPUTResponse("/api/devices/device-123", "{}");
        test.ConfigureUser();

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        Assert.Equal("tester", test.Plugin.Settings.AccountState.User);
        Assert.Null(test.Plugin.Settings.AccountState.LastAuthenticated);
        await test.RomM.Server.DidNotReceive().GETAsync("/api/platforms", false);
    }

    [Fact]
    public async Task SyncPlatforms_ExposesOnlyPlatformsWithROMsBeforeRefresh()
    {
        using var test = new AuthenticationTestHarness();
        var mapping = new EmulatorMapping();
        test.Plugin.Settings.Mappings.Add(mapping);
        test.Plugin.Settings.RomMPlatforms.Add(new RomMPlatform { Id = 1, RomCount = 3 });
        test.Plugin.Settings.RomMPlatforms.Add(new RomMPlatform { Id = 2, RomCount = 0 });

        var result = await test.Authentication.SyncPlatforms();

        Assert.False(result);
        Assert.Equal(1, Assert.Single(mapping.AvailablePlatforms).Id);
    }

    [Theory]
    [InlineData("5.4.0-alpha.1")]
    [InlineData("5.4.0-alpha.2")]
    [InlineData("5.4.0-beta.1")]
    public async Task Login_WhenRegistrationFails_PreservesPrereleaseServerVersion(string version)
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureToken();
        test.ConfigureHeartbeat(version);
        test.RomM.ConfigureJsonPOSTResponse("/api/devices", null);

        var result = await test.Authentication.Login(suppressNotification: true);

        Assert.False(result);
        Assert.Equal(version, test.Plugin.Settings.AccountState.ServerVersion);
    }
}
