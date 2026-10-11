using NSubstitute;

using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Graviton.Tests.Fakes.RomM;

public sealed class RomMServerFake
{
    private readonly Dictionary<(string Method, string Path), string?> _jsonResponses = new();
    private readonly Dictionary<(string Method, string Path), (byte[] Content, HttpStatusCode StatusCode)> _rawResponses = new();

    public IRomMServer Server { get; } = Substitute.For<IRomMServer>();

    public RomMServerFake()
    {
        // Fake responses
        Server.GETAsync(Arg.Any<string>(), Arg.Any<bool>()).Returns(call => CreateJsonResponse("GET", call.ArgAt<string>(0)));

        Server.POSTAsync(Arg.Any<string>(), Arg.Any<HttpContent?>(), Arg.Any<bool>()).Returns(call => CreateJsonResponse("POST", call.ArgAt<string>(0)));
        Server.POSTAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<bool>()).Returns(call => CreateJsonResponse("POST", call.ArgAt<string>(0)));

        Server.PUTAsync(Arg.Any<string>(), Arg.Any<HttpContent?>(), Arg.Any<bool>()).Returns(call => CreateJsonResponse("PUT", call.ArgAt<string>(0)));
        Server.PUTAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<bool>()).Returns(call => CreateJsonResponse("PUT", call.ArgAt<string>(0)));

        Server.DELETEAsync(Arg.Any<string>(), Arg.Any<bool>()).Returns(call => CreateJsonResponse("DELETE", call.ArgAt<string>(0)));

        // Fake raw responses
        Server.RawGETAsync(Arg.Any<string>(), Arg.Any<bool>()).Returns(call => CreateRawResponse("GET", call.ArgAt<string>(0)));

        Server.RawPOSTAsync(Arg.Any<string>(), Arg.Any<HttpContent>(), Arg.Any<bool>()).Returns(call => CreateRawResponse("POST", call.ArgAt<string>(0)));
        Server.RawPOSTAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<bool>()).Returns(call => CreateRawResponse("POST", call.ArgAt<string>(0)));

        Server.RawPUTAsync(Arg.Any<string>(), Arg.Any<HttpContent>(), Arg.Any<bool>()).Returns(call => CreateRawResponse("PUT", call.ArgAt<string>(0)));
        Server.RawPUTAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<bool>()).Returns(call => CreateRawResponse("PUT", call.ArgAt<string>(0)));
    }

    public void ConfigureJsonGETResponse(string path, string? json)
    {
        _jsonResponses[("GET", path)] = json;
    }

    public void ConfigureJsonPOSTResponse(string path, string? json = null)
    {
        _jsonResponses[("POST", path)] = json;
    }

    public void ConfigureJsonPUTResponse(string path, string? json = null)
    {
        _jsonResponses[("PUT", path)] = json;
    }

    public void ConfigureJsonDELETEResponse(string path, string? json = null)
    {
        _jsonResponses[("DELETE", path)] = json;
    }

    public void ConfigureRawGETResponse(string path, byte[] content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _rawResponses[("GET", path)] = (content.ToArray(), statusCode);
    }

    public void ConfigureRawPOSTResponse(string path, byte[] content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _rawResponses[("POST", path)] = (content.ToArray(), statusCode);
    }

    public void ConfigureRawPUTResponse(string path, byte[] content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _rawResponses[("PUT", path)] = (content.ToArray(), statusCode);
    }

    private Task<JsonDocument?> CreateJsonResponse(string method, string path)
    {
        if (!_jsonResponses.TryGetValue((method, path), out var json))
        {
            throw new InvalidOperationException($"No RomM {method} JSON response configured for '{path}'.");
        }

        return Task.FromResult<JsonDocument?>(json is null ? null : JsonDocument.Parse(json));
    }

    private Task<RawClientResponse?> CreateRawResponse(string method, string path)
    {
        if (!_rawResponses.TryGetValue((method, path), out var configured))
        {
            throw new InvalidOperationException($"No RomM raw {method} response configured for '{path}'.");
        }

        var response = new HttpResponseMessage(configured.StatusCode)
        {
            Content = new ByteArrayContent(configured.Content.ToArray())
        };

        return Task.FromResult<RawClientResponse?>(new RawClientResponse(response));
    }
}
