using Graviton.Install;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;
using NSubstitute.Extensions;

using System.Net.Http;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class GravitonRemoteInstallControllerTests
{
    private const string ClaimPath = "/api/devices/test-device/installs/claim";

    [Fact]
    public async Task InstallQueued_WhenNoRequestsArePending_OnlyClaimsRequests()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonPOSTResponse(ClaimPath, "[]");

        await controller.InstallQueued();

        await romm.Server.Received(1).POSTAsync(
            APIPath: ClaimPath,
            content: (HttpContent?)null,
            PublicEndpoint: false);

        await romm.Server.DidNotReceive().GETAsync(
            APIPath: Arg.Any<string>(),
            PublicEndpoint: Arg.Any<bool>());
    }

    [Fact]
    public async Task InstallQueued_WhenClaimReturnsNull_AllowsSubsequentClaims()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonPOSTResponse(ClaimPath, null);

        await controller.InstallQueued();

        romm.ConfigureJsonPOSTResponse(ClaimPath, "[]");
        await controller.InstallQueued();

        await romm.Server.Received(2).POSTAsync(
            APIPath: ClaimPath,
            content: (HttpContent?)null,
            PublicEndpoint: false);
    }

    [Fact]
    public async Task Connect_WhenUsingBasicAuthentication_DoesNotClaimRemoteInstalls()
    {
        var (controller, plugin, romm) = CreateController();
        plugin.Settings.UseBasicAuth = true;

        await controller.Connect();

        await romm.Server.DidNotReceive().POSTAsync(
            APIPath: Arg.Any<string>(),
            content: Arg.Any<HttpContent?>(),
            PublicEndpoint: Arg.Any<bool>());
    }

    [Fact]
    public async Task InstallQueued_WhenClaimResponseHasUnexpectedShape_AllowsNextClaim()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonPOSTResponse(ClaimPath, """{"unexpected":"object"}""");

        await controller.InstallQueued();

        romm.ConfigureJsonPOSTResponse(ClaimPath, "[]");
        await controller.InstallQueued();

        await romm.Server.Received(2).POSTAsync(
            APIPath: ClaimPath,
            content: (HttpContent?)null,
            PublicEndpoint: false);
    }

    [Fact]
    public async Task InstallQueued_WhenClaimRequestFails_AllowsNextClaim()
    {
        var (controller, _, romm) = CreateController();
        var claimCount = 0;

        romm.Server.Configure().POSTAsync(APIPath: ClaimPath,
                                          content: (HttpContent?)null,
                                          PublicEndpoint: false).Returns(call =>
        {
            claimCount++;

            if (claimCount == 1)
            {
                return Task.FromException<JsonDocument?>(new HttpRequestException("Simulated request failure"));
            }

            return Task.FromResult<JsonDocument?>(JsonDocument.Parse("[]"));
        });

        await controller.InstallQueued();
        await controller.InstallQueued();

        Assert.Equal(2, claimCount);
    }

    [Fact]
    public async Task InstallQueued_WhenCalledConcurrently_DoesNotOverlapClaims()
    {
        var (controller, _, romm) = CreateController();
        var firstClaimCompletion = new TaskCompletionSource<JsonDocument?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var claimCount = 0;

        romm.Server.Configure().POSTAsync(APIPath: ClaimPath,
                                          content: (HttpContent?)null,
                                          PublicEndpoint: false).Returns(call =>
        {
            var currentClaim = Interlocked.Increment(ref claimCount);

            if (currentClaim == 1)
            {
                return firstClaimCompletion.Task;
            }

            return Task.FromResult<JsonDocument?>(JsonDocument.Parse("[]"));
        });

        var first = controller.InstallQueued();
        var second = controller.InstallQueued();

        var claimsBeforeRelease = Volatile.Read(ref claimCount);
        firstClaimCompletion.SetResult(JsonDocument.Parse("[]"));

        await Task.WhenAll(first, second);

        Assert.Equal(1, claimsBeforeRelease);
        Assert.Equal(2, Volatile.Read(ref claimCount));
    }

    private static (GravitonRemoteInstallController Controller, GravitonContextFake Plugin, RomMServerFake RomM) CreateController()
    {
        var plugin = new GravitonContextFake();
        plugin.Settings.Host = "https://romm.example";
        plugin.Settings.AccountState.DeviceID = "test-device";

        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();

        var controller = new GravitonRemoteInstallController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            server: romm.Server);

        return (controller, plugin, romm);
    }
}
