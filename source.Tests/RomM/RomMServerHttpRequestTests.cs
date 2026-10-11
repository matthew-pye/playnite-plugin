using Graviton.Tests.Fakes.RomM;

using System.Net.Http;
using System.Text;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.RomM;

[CollectionDefinition("RomMRealHttpClient", DisableParallelization = true)]
public sealed class RomMRealHttpClientCollection
{
}

[Collection("RomMRealHttpClient")]
public sealed class RomMServerHttpRequestTests
{
    [Fact]
    public async Task GET_UsesConfiguredHostPathAndAcceptHeader()
    {
        using var test = new RomMHttpTestHarness();
        test.Reply("{\"id\":42}");

        using var document = await test.Server.GETAsync("/api/roms/42?include=files");

        Assert.NotNull(document);
        Assert.Equal(42, document.RootElement.GetProperty("id").GetInt32());
        var request = Assert.Single(test.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://romm.example.test/api/roms/42?include=files", request.Address.AbsoluteUri);
        Assert.Contains("application/json", request.Header("Accept") ?? "");
    }

    [Fact]
    public async Task POST_WithObject_SendsJsonAndContentType()
    {
        using var test = new RomMHttpTestHarness();
        test.Reply("{\"created\":true}");

        using var document = await test.Server.POSTAsync("/api/saves", new { rom_id = 42, slot = "auto" });

        Assert.NotNull(document);
        Assert.True(document.RootElement.GetProperty("created").GetBoolean());
        var request = Assert.Single(test.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(42, body.RootElement.GetProperty("rom_id").GetInt32());
        Assert.Equal("auto", body.RootElement.GetProperty("slot").GetString());
    }

    [Fact]
    public async Task POST_WithContent_PreservesCallerProvidedPayloadAndMediaType()
    {
        using var test = new RomMHttpTestHarness();
        using var content = new StringContent("sample=payload", Encoding.UTF8, "application/x-www-form-urlencoded");

        using var document = await test.Server.POSTAsync("/api/upload", content);

        var request = Assert.Single(test.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("sample=payload", request.Body);
        Assert.Equal("application/x-www-form-urlencoded", request.ContentType);
    }

    [Fact]
    public async Task PUT_WithObject_SendsJson()
    {
        using var test = new RomMHttpTestHarness();

        using var document = await test.Server.PUTAsync("/api/saves/3", new { name = "updated" });

        var request = Assert.Single(test.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("updated", body.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PUT_WithContent_PreservesCallerPayload()
    {
        using var test = new RomMHttpTestHarness();
        using var content = new StringContent("raw update");

        using var document = await test.Server.PUTAsync("/api/saves/3", content);

        var request = Assert.Single(test.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("raw update", request.Body);
    }

    [Fact]
    public async Task DELETE_UsesDeleteMethod()
    {
        using var test = new RomMHttpTestHarness();
        test.ReplyEmpty();

        using var document = await test.Server.DELETEAsync("/api/saves/123");

        Assert.Null(document);
        var request = Assert.Single(test.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/saves/123", request.Address.AbsolutePath);
    }

    [Fact]
    public async Task HEAD_UsesHeadMethodAndDoesNotRequireResponseBody()
    {
        using var test = new RomMHttpTestHarness();
        test.ReplyEmpty();

        using var document = await test.Server.HEADAsync("/api/heartbeat");

        Assert.Null(document);
        Assert.Equal(HttpMethod.Head, Assert.Single(test.Requests).Method);
    }

    [Fact]
    public async Task ConsecutiveRequests_DoNotRetainPreviousRequestBody()
    {
        using var test = new RomMHttpTestHarness();
        test.Reply("{\"ok\":true}");

        using var first = await test.Server.POSTAsync("/api/saves", new { slot = "first" });
        using var second = await test.Server.GETAsync("/api/saves");

        Assert.Equal(2, test.Requests.Count);
        Assert.Contains("first", Assert.IsType<string>(test.Requests[0].Body));
        Assert.Null(test.Requests[1].Body);
    }

    [Fact]
    public async Task RawPOST_WithObject_SendsJson()
    {
        using var test = new RomMHttpTestHarness();

        using var response = await test.Server.RawPOSTAsync("/api/saves", new { slot = "backup" });

        Assert.NotNull(response);
        var request = Assert.Single(test.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("backup", body.RootElement.GetProperty("slot").GetString());
    }

    [Fact]
    public async Task RawPUT_WithContent_SendsCallerProvidedBytes()
    {
        using var test = new RomMHttpTestHarness();
        using var content = new ByteArrayContent(new byte[] { 1, 2, 3 });

        using var response = await test.Server.RawPUTAsync("/api/saves/123", content);

        Assert.NotNull(response);
        Assert.Equal(HttpMethod.Put, Assert.Single(test.Requests).Method);
    }
}
