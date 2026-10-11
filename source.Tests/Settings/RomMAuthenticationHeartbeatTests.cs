using Graviton.Tests.Fakes.Settings;

using NSubstitute;

using Xunit;

namespace Graviton.Tests.Settings;

[CollectionDefinition("RomMAuthentication", DisableParallelization = true)]
public sealed class RomMAuthenticationCollection
{
}

[Collection("RomMAuthentication")]
public sealed class RomMAuthenticationHeartbeatTests
{
    [Fact]
    public async Task Heartbeat_ParsesVersionFromSystemObject()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureHeartbeat("5.4.0");

        var info = await test.Authentication.Heartbeat();

        Assert.NotNull(info);
        Assert.Equal("5.4.0", info.Value.Version);
        Assert.False(info.Value.ShowSetupWizard);
        await test.RomM.Server.Received(1).GETAsync("/api/heartbeat", true);
    }

    [Fact]
    public async Task Heartbeat_ParsesSetupWizardFlag()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonGETResponse("/api/heartbeat", """
            {"SYSTEM":{"VERSION":"5.4.0","SHOW_SETUP_WIZARD":true}}
            """);

        var info = await test.Authentication.Heartbeat();

        Assert.NotNull(info);
        Assert.True(info.Value.ShowSetupWizard);
    }

    [Fact]
    public async Task Heartbeat_WhenServerHasNoResponse_ReturnsNull()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonGETResponse("/api/heartbeat", null);

        var info = await test.Authentication.Heartbeat();

        Assert.Null(info);
    }

    [Fact]
    public async Task Heartbeat_WithoutSystemObject_ReturnsNull()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonGETResponse("/api/heartbeat", """
            {"VERSION":"5.4.0"}
            """);

        var info = await test.Authentication.Heartbeat();

        Assert.Null(info);
    }

    [Fact]
    public async Task Heartbeat_WithWrongSystemShape_ReturnsNull()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonGETResponse("/api/heartbeat", """
            {"SYSTEM":[]}
            """);

        var info = await test.Authentication.Heartbeat();

        Assert.Null(info);
    }

    [Fact]
    public async Task Heartbeat_WithUnexpectedVersionType_ReturnsNull()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonGETResponse("/api/heartbeat", """
            {"SYSTEM":{"VERSION":42,"SHOW_SETUP_WIZARD":false}}
            """);

        var info = await test.Authentication.Heartbeat();

        Assert.Null(info);
    }

    [Theory]
    [InlineData("5.4.0-alpha.1")]
    [InlineData("5.4.0-alpha.2")]
    [InlineData("5.4.0-beta.1")]
    public async Task Heartbeat_PreservesPrereleaseVersion(string version)
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureHeartbeat(version);

        var info = await test.Authentication.Heartbeat();

        Assert.NotNull(info);
        Assert.Equal(version, info.Value.Version);
    }
}
