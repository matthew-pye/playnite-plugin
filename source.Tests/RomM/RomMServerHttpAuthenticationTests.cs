using Graviton.Tests.Fakes.RomM;

using System.Net;
using System.Text;

using Xunit;

namespace Graviton.Tests.RomM;

[Collection("RomMRealHttpClient")]
public sealed class RomMServerHttpAuthenticationTests
{
    [Fact]
    public async Task BearerToken_IsSentOnAuthenticatedRequests()
    {
        using var test = new RomMHttpTestHarness();
        test.Server.ConfigureClientToken("device-token-123");

        using var document = await test.Server.GETAsync("/api/me");

        Assert.Equal("Bearer device-token-123", Assert.Single(test.Requests).Header("Authorization"));
    }

    [Fact]
    public async Task BasicAuthentication_UsesUtf8Credentials()
    {
        using var test = new RomMHttpTestHarness();
        test.Server.ConfigureBasicAuth("jose", "påssword");

        using var document = await test.Server.GETAsync("/api/me");

        var header = Assert.Single(test.Requests).Header("Authorization");
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("jose:påssword"));
        Assert.Equal($"Basic {expected}", header);
    }

    [Fact]
    public async Task SwitchingFromBasicToBearer_ReplacesAuthorizationScheme()
    {
        using var test = new RomMHttpTestHarness();
        test.Server.ConfigureBasicAuth("user", "password");
        test.Server.ConfigureClientToken("new-token");

        using var document = await test.Server.GETAsync("/api/me");

        Assert.Equal("Bearer new-token", Assert.Single(test.Requests).Header("Authorization"));
    }

    [Fact]
    public async Task SwitchingFromBearerToBasic_ReplacesAuthorizationScheme()
    {
        using var test = new RomMHttpTestHarness();
        test.Server.ConfigureClientToken("old-token");
        test.Server.ConfigureBasicAuth("user", "password");

        using var document = await test.Server.GETAsync("/api/me");

        Assert.StartsWith("Basic ", Assert.IsType<string>(Assert.Single(test.Requests).Header("Authorization")));
    }

    [Fact]
    public async Task CustomCloudflareHeaders_AreAppliedWithAuthentication()
    {
        using var test = new RomMHttpTestHarness();
        test.AddCustomHeader("CF-Access-Client-Id", "test-client-id");
        test.AddCustomHeader("CF-Access-Client-Secret", "test-secret");
        test.Server.ConfigureClientToken("romm-token");

        using var document = await test.Server.GETAsync("/api/roms");

        var request = Assert.Single(test.Requests);
        Assert.Equal("test-client-id", request.Header("CF-Access-Client-Id"));
        Assert.Equal("test-secret", request.Header("CF-Access-Client-Secret"));
        Assert.Equal("Bearer romm-token", request.Header("Authorization"));
    }

    [Fact]
    public async Task DisabledCustomHeader_IsNotApplied()
    {
        using var test = new RomMHttpTestHarness();
        test.AddCustomHeader("X-Test-Disabled", "should-not-send", enabled: false);
        test.Server.ConfigureClientToken("romm-token");

        using var document = await test.Server.GETAsync("/api/roms");

        Assert.Null(Assert.Single(test.Requests).Header("X-Test-Disabled"));
    }

    [Fact]
    public async Task AddHeader_DoesNotOverwriteExistingHeader()
    {
        using var test = new RomMHttpTestHarness();
        test.Server.AddHeader("X-Request-Source", "first");
        test.Server.AddHeader("X-Request-Source", "second");

        using var document = await test.Server.GETAsync("/api/heartbeat");

        Assert.Equal("first", Assert.Single(test.Requests).Header("X-Request-Source"));
    }

    [Fact]
    public async Task RemoveHeader_StopsSendingIt()
    {
        using var test = new RomMHttpTestHarness();
        test.Server.AddHeader("X-Custom-Header", "value");
        test.Server.RemoveHeader("X-Custom-Header");

        using var document = await test.Server.GETAsync("/api/heartbeat");

        Assert.Null(Assert.Single(test.Requests).Header("X-Custom-Header"));
    }

    [Fact]
    public async Task ConfigureTimeout_PreservesBearerAndConfiguredCloudflareHeader()
    {
        using var test = new RomMHttpTestHarness();
        test.AddCustomHeader("CF-Access-Client-Id", "client-id");
        test.Server.ConfigureClientToken("token");

        test.Server.ConfigureTimeout(17);
        using var document = await test.Server.GETAsync("/api/heartbeat");

        Assert.Equal(2, test.Clients.Count);
        Assert.Equal(TimeSpan.FromSeconds(17), test.Clients[1].Timeout);
        var request = Assert.Single(test.Requests);
        Assert.Equal("Bearer token", request.Header("Authorization"));
        Assert.Equal("client-id", request.Header("CF-Access-Client-Id"));
        Assert.Contains("application/json", request.Header("Accept") ?? "");
    }

    [Fact]
    public async Task ConfigureTimeout_PreservesBasicCredentials()
    {
        using var test = new RomMHttpTestHarness();
        test.Server.ConfigureBasicAuth("user", "password");
        test.Server.ConfigureTimeout(8);

        using var document = await test.Server.GETAsync("/api/me");

        Assert.StartsWith("Basic ", Assert.IsType<string>(Assert.Single(test.Requests).Header("Authorization")));
        Assert.Equal(TimeSpan.FromSeconds(8), test.Clients[1].Timeout);
    }

    [Fact]
    public async Task UnauthenticatedPrivateRequest_IsRejectedBeforeHttpSend()
    {
        using var test = new RomMHttpTestHarness(authenticated: false);
        test.Plugin.Settings.AccountState.User = "signed-out-user";
        test.Plugin.Settings.AccountState.UserType = "admin";

        using var document = await test.Server.GETAsync("/api/me");

        Assert.Null(document);
        Assert.Empty(test.Requests);
        Assert.Equal("----", test.Plugin.Settings.AccountState.User);
        Assert.Equal("----", test.Plugin.Settings.AccountState.UserType);
    }

    [Fact]
    public async Task UnauthenticatedPublicRequest_IsAllowed()
    {
        using var test = new RomMHttpTestHarness(authenticated: false);
        test.Reply("{\"status\":\"ok\"}");

        using var document = await test.Server.GETAsync("/api/heartbeat", PublicEndpoint: true);

        Assert.NotNull(document);
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
        Assert.Single(test.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task RawUnauthorizedResponse_ResetsAuthenticationState(HttpStatusCode status)
    {
        using var test = new RomMHttpTestHarness();
        test.Plugin.Settings.AccountState.User = "saved-user";
        test.Plugin.Settings.AccountState.UserType = "admin";
        test.ReplyEmpty(status);

        using var response = await test.Server.RawGETAsync("/api/me");

        Assert.NotNull(response);
        Assert.Equal(status, response.Status);
        Assert.Null(test.Plugin.Settings.AccountState.LastAuthenticated);
        Assert.Equal("----", test.Plugin.Settings.AccountState.User);
        Assert.Equal(status, test.Plugin.Settings.AccountState.AuthenticateFailed);
    }
}
