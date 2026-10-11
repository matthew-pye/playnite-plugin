using Graviton.Tests.Fakes.RomM;

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.RomM;

[Collection("RomMRealHttpClient")]
public sealed class RomMServerHttpResponseTests
{
    [Fact]
    public async Task ValidJson_UpdatesAuthenticationFailureStateToOK()
    {
        using var test = new RomMHttpTestHarness();
        test.Plugin.Settings.AccountState.AuthenticateFailed = HttpStatusCode.BadRequest;
        test.Reply("[1,2,3]");

        using var document = await test.Server.GETAsync("/api/roms");

        Assert.NotNull(document);
        Assert.Equal(3, document.RootElement.GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, test.Plugin.Settings.AccountState.AuthenticateFailed);
    }

    [Fact]
    public async Task EmptySuccessBody_ReturnsNull()
    {
        using var test = new RomMHttpTestHarness();
        test.ReplyEmpty();

        using var document = await test.Server.GETAsync("/api/roms");

        Assert.Null(document);
        Assert.Single(test.Requests);
    }

    [Fact]
    public async Task MalformedJson_ReturnsNullWithoutThrowing()
    {
        using var test = new RomMHttpTestHarness();
        test.Reply("{invalid json");

        using var document = await test.Server.GETAsync("/api/roms");

        Assert.Null(document);
        Assert.Single(test.Requests);
    }

    [Fact]
    public async Task BadRequest_ReturnsNullButKeepsAuthenticatedSession()
    {
        using var test = new RomMHttpTestHarness();
        test.Reply("{\"detail\":\"Invalid request\"}", HttpStatusCode.BadRequest);

        using var document = await test.Server.GETAsync("/api/roms?bad=true");

        Assert.Null(document);
        Assert.NotNull(test.Plugin.Settings.AccountState.LastAuthenticated);
        Assert.Single(test.Requests);
    }

    [Fact]
    public async Task InternalServerError_ReturnsNullWithoutThrowing()
    {
        using var test = new RomMHttpTestHarness();
        test.Reply("{\"detail\":\"Server failure\"}", HttpStatusCode.InternalServerError);

        using var document = await test.Server.GETAsync("/api/roms");

        Assert.Null(document);
        Assert.Single(test.Requests);
    }

    [Fact]
    public async Task HttpHandlerException_ReturnsNullWithoutThrowing()
    {
        using var test = new RomMHttpTestHarness();
        test.Respond = (request, cancellationToken) =>
        {
            throw new HttpRequestException("Connection refused");
        };

        using var document = await test.Server.GETAsync("/api/roms");

        Assert.Null(document);
        Assert.Single(test.Requests);
    }

    [Fact]
    public async Task RawResponse_PreservesBinaryDataAndContentType()
    {
        using var test = new RomMHttpTestHarness();
        var bytes = new byte[] { 0x00, 0xFF, 0x01, 0x42 };
        test.ReplyBytes(bytes);

        using var response = await test.Server.RawGETAsync("/api/saves/42/content");

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, test.Plugin.Settings.AccountState.AuthenticateFailed);
    }

    [Fact]
    public async Task RawNotFound_PreservesStatusAndResponseBody()
    {
        using var test = new RomMHttpTestHarness();
        test.Reply("{\"detail\":\"missing\"}", HttpStatusCode.NotFound);

        using var response = await test.Server.RawGETAsync("/api/saves/999/content");

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("missing", body);
    }

    [Fact]
    public async Task RawHead_PreservesResponseStatus()
    {
        using var test = new RomMHttpTestHarness();
        test.ReplyEmpty(HttpStatusCode.NoContent);

        using var response = await test.Server.RawHEADAsync("/api/heartbeat");

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.NoContent, response.Status);
        Assert.Equal(HttpMethod.Head, Assert.Single(test.Requests).Method);
    }

    [Fact]
    public async Task RawPost_WithContent_UsesProvidedPayload()
    {
        using var test = new RomMHttpTestHarness();
        using var content = new StringContent("binary-like-body", Encoding.UTF8, "application/octet-stream");

        using var response = await test.Server.RawPOSTAsync("/api/saves", content);

        Assert.NotNull(response);
        Assert.Equal("binary-like-body", Assert.Single(test.Requests).Body);
        Assert.Equal("application/octet-stream", Assert.Single(test.Requests).ContentType);
    }

    [Fact]
    public async Task RawPut_WithObject_UsesJsonMediaType()
    {
        using var test = new RomMHttpTestHarness();

        using var response = await test.Server.RawPUTAsync("/api/saves/42", new { slot = "manual" });

        Assert.NotNull(response);
        var request = Assert.Single(test.Requests);
        Assert.Equal("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("manual", body.RootElement.GetProperty("slot").GetString());
    }

    [Fact]
    public async Task RawHandlerException_ReturnsNullWithoutThrowing()
    {
        using var test = new RomMHttpTestHarness();
        test.Respond = (request, cancellationToken) =>
        {
            throw new HttpRequestException("Connection reset");
        };

        using var response = await test.Server.RawGETAsync("/api/saves/1/content");

        Assert.Null(response);
        Assert.Single(test.Requests);
    }
}
