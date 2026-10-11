using System.Net;
using System.Net.Http;
using Xunit;

namespace Graviton.Tests.Fakes.RomM;

public sealed class RomMServerFakeTests
{
    [Fact]
    public async Task GETAsync_ReturnsConfiguredJson()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonGETResponse("/api/platforms", """[{"id":1,"name":"SNES"}]""");

        using var response = await romm.Server.GETAsync("/api/platforms");

        Assert.NotNull(response);
        Assert.Equal(1, response.RootElement.GetArrayLength());
        Assert.Equal("SNES", response.RootElement[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GETAndPOST_UseSeparateResponsesForTheSamePath()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonGETResponse("/api/saves", "[]");
        romm.ConfigureJsonPOSTResponse("/api/saves", """{"id":123}""");

        using var getResponse = await romm.Server.GETAsync("/api/saves");
        using var postResponse = await romm.Server.POSTAsync("/api/saves", new { Name = "Save" });

        Assert.NotNull(getResponse);
        Assert.NotNull(postResponse);
        Assert.Equal(0, getResponse.RootElement.GetArrayLength());
        Assert.Equal(123, postResponse.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task POSTAsync_WithHttpContent_ReturnsConfiguredJson()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/saves", """{"id":456}""");
        using var content = new StringContent("test");

        using var response = await romm.Server.POSTAsync("/api/saves", content);

        Assert.NotNull(response);
        Assert.Equal(456, response.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task DELETEAsync_WithoutJson_ReturnsNull()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonDELETEResponse("/api/saves/123");

        using var response = await romm.Server.DELETEAsync("/api/saves/123");

        Assert.Null(response);
    }

    [Fact]
    public async Task RawGETAsync_ReturnsConfiguredBytesAndStatus()
    {
        var romm = new RomMServerFake();
        var bytes = new byte[] { 0x00, 0xFF, 0x42, 0x80 };
        romm.ConfigureRawGETResponse("/api/saves/123/content", bytes, HttpStatusCode.PartialContent);

        using var response = await romm.Server.RawGETAsync("/api/saves/123/content");

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.PartialContent, response.Status);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RawGETAsync_ReturnsAnIndependentResponseEachTime()
    {
        var romm = new RomMServerFake();
        var bytes = new byte[] { 0x01, 0x02, 0x03 };
        romm.ConfigureRawGETResponse("/api/saves/123/content", bytes);
        bytes[0] = 0xFF;

        using (var first = await romm.Server.RawGETAsync("/api/saves/123/content"))
        {
            Assert.NotNull(first);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, await first.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }

        using var second = await romm.Server.RawGETAsync("/api/saves/123/content");

        Assert.NotNull(second);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, await second.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GETAsync_WithoutConfiguredResponse_ThrowsHelpfulError()
    {
        var romm = new RomMServerFake();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await romm.Server.GETAsync("/api/missing");
        });

        Assert.Contains("/api/missing", exception.Message);
    }
}
