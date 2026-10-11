using Graviton.Models;
using Graviton.Notifications;
using Graviton.Settings;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;

using NSubstitute;

using Playnite;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Graviton.Tests.Fakes.RomM;

internal sealed class RomMHttpTestHarness : IDisposable
{
    private readonly IPlayniteApi _previousLocalizationApi;
    private readonly RecordingRomMHandler _handler;
    private readonly List<HttpClient> _createdClients = new();

    public GravitonContextFake Plugin { get; } = new();
    public PlayniteFake Playnite { get; } = new();
    public RomMServer Server { get; }
    public IReadOnlyList<HttpClient> Clients => _createdClients;
    public IReadOnlyList<RecordedRomMRequest> Requests => _handler.Requests;
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; }

    public RomMHttpTestHarness(bool authenticated = true)
    {
        _previousLocalizationApi = Loc.Api;
        Loc.Api = Playnite.Api;
        Plugin.Settings.Host = "https://romm.example.test/";
        Plugin.Settings.ServerTimeout = 30;
        Plugin.Settings.AccountState.LastAuthenticated = authenticated ? DateTime.UtcNow : null;
        Plugin.Context.PluginDLLPath.Returns("C:\\Graviton");
        var logger = new GravitonLogger(Plugin.Settings);

        Respond = (request, cancellationToken) =>
        {
            return Task.FromResult(JsonResponse("{}"));
        };

        _handler = new RecordingRomMHandler((request, cancellationToken) =>
        {
            return Respond(request, cancellationToken);
        });

        Server = new RomMServer(Plugin.Context, Playnite.Api, logger, () =>
        {
            var client = new HttpClient(_handler, disposeHandler: false);
            _createdClients.Add(client);
            return client;
        });

        var account = new RomMAuthentication(Plugin.Context, Playnite.Api, logger, Server);
        Plugin.Context.Account.Returns(account);
    }

    public void Reply(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        Respond = (request, cancellationToken) =>
        {
            return Task.FromResult(JsonResponse(json, status));
        };
    }

    public void ReplyEmpty(HttpStatusCode status = HttpStatusCode.NoContent)
    {
        Respond = (request, cancellationToken) =>
        {
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            });
        };
    }

    public void ReplyBytes(byte[] bytes, HttpStatusCode status = HttpStatusCode.OK)
    {
        Respond = (request, cancellationToken) =>
        {
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new ByteArrayContent(bytes)
            });
        };
    }

    public void AddCustomHeader(string name, string value, bool enabled = true)
    {
        Plugin.Settings.CustomHeaders.Add(new CustomHTTPHeader
        {
            Name = name,
            Value = value,
            Enabled = enabled
        });
    }

    public static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    public void Dispose()
    {
        Loc.Api = _previousLocalizationApi;
        foreach (var client in _createdClients)
        {
            client.Dispose();
        }
        _handler.Dispose();
    }
}

internal sealed class RecordedRomMRequest
{
    private readonly Dictionary<string, string[]> _headers;

    public HttpMethod Method { get; }
    public Uri Address { get; }
    public string? Body { get; }
    public string? ContentType { get; }

    public RecordedRomMRequest(HttpRequestMessage request, string? body)
    {
        Method = request.Method;
        Address = request.RequestUri!;
        Body = body;
        ContentType = request.Content?.Headers.ContentType?.MediaType;
        _headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            _headers[header.Key] = header.Value.ToArray();
        }
    }

    public string? Header(string name)
    {
        return _headers.TryGetValue(name, out var values) ? string.Join(",", values) : null;
    }
}

internal sealed class RecordingRomMHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly ConcurrentQueue<RecordedRomMRequest> _requests = new();

    public IReadOnlyList<RecordedRomMRequest> Requests => _requests.ToArray();

    public RecordingRomMHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue(new RecordedRomMRequest(request, body));
        return await _respond(request, cancellationToken);
    }
}
