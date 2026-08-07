using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Graviton.Tests.Fakes
{
    public sealed record RomMRequest(HttpMethod Method, string Path, bool PublicEndpoint, object? Body);

    public class FakeRomMServer : IRomMServer
    {
        private sealed record JsonRule(HttpMethod Method, string PathPattern, Func<RomMRequest, JsonDocument?> Responder);
        private sealed record RawRule(HttpMethod Method, string PathPattern, Func<RomMRequest, RawClientResponse?> Responder);

        private readonly List<JsonRule> _jsonRules = new();
        private readonly List<RawRule> _rawRules = new();

        public List<RomMRequest> Requests { get; } = new();

        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public (string Username, string Password)? BasicAuth { get; private set; }
        public string? ClientToken { get; private set; }

        public void AddHeader(string name, string value) => Headers[name] = value;

        public void RemoveHeader(string name) => Headers.Remove(name);

        public void ConfigureBasicAuth(string username, string password)
        {
            BasicAuth = (username, password);
            ClientToken = null;
        }

        public void ConfigureClientToken(string clientToken)
        {
            ClientToken = clientToken;
            BasicAuth = null;
        }

        public Task<JsonDocument?> GETAsync(string APIPath, bool PublicEndpoint = false) =>
            Task.FromResult(HandleJson(HttpMethod.Get, APIPath, PublicEndpoint, null));

        public Task<JsonDocument?> POSTAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) =>
            Task.FromResult(HandleJson(HttpMethod.Post, APIPath, PublicEndpoint, content));

        public Task<JsonDocument?> POSTAsync(string APIPath, object json, bool PublicEndpoint = false) =>
            Task.FromResult(HandleJson(HttpMethod.Post, APIPath, PublicEndpoint, json));

        public Task<JsonDocument?> PUTAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) =>
            Task.FromResult(HandleJson(HttpMethod.Put, APIPath, PublicEndpoint, content));

        public Task<JsonDocument?> PUTAsync(string APIPath, object json, bool PublicEndpoint = false) =>
            Task.FromResult(HandleJson(HttpMethod.Put, APIPath, PublicEndpoint, json));

        public Task<JsonDocument?> DELETEAsync(string APIPath, bool PublicEndpoint = false) =>
            Task.FromResult(HandleJson(HttpMethod.Delete, APIPath, PublicEndpoint, null));

        public Task<RawClientResponse?> RawGETAsync(string APIPath, bool PublicEndpoint = false) =>
            Task.FromResult(HandleRaw(HttpMethod.Get, APIPath, PublicEndpoint, null));

        public Task<RawClientResponse?> RawPOSTAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) =>
            Task.FromResult(HandleRaw(HttpMethod.Post, APIPath, PublicEndpoint, content));

        public Task<RawClientResponse?> RawPOSTAsync(string APIPath, object json, bool PublicEndpoint = false) =>
            Task.FromResult(HandleRaw(HttpMethod.Post, APIPath, PublicEndpoint, json));

        public Task<RawClientResponse?> RawPUTAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) =>
            Task.FromResult(HandleRaw(HttpMethod.Put, APIPath, PublicEndpoint, content));

        public Task<RawClientResponse?> RawPUTAsync(string APIPath, object json, bool PublicEndpoint = false) =>
            Task.FromResult(HandleRaw(HttpMethod.Put, APIPath, PublicEndpoint, json));

        public void RespondTo(HttpMethod method, string pathPattern, JsonDocument? response) =>
            _jsonRules.Add(new JsonRule(method, pathPattern, _ => response));

        public void RespondTo(HttpMethod method, string pathPattern, string json) =>
            RespondTo(method, pathPattern, JsonDocument.Parse(json));

        public void RespondTo(HttpMethod method, string pathPattern, object value) =>
            RespondTo(method, pathPattern, JsonSerializer.SerializeToDocument(value));

        public void RespondTo(HttpMethod method, string pathPattern, Func<RomMRequest, JsonDocument?> responder) =>
            _jsonRules.Add(new JsonRule(method, pathPattern, responder));

        public void RespondToRaw(HttpMethod method, string pathPattern, HttpStatusCode status, HttpContent? content = null) =>
            _rawRules.Add(new RawRule(method, pathPattern, _ => new RawClientResponse { Status = status, Content = content }));

        public void RespondToRaw(HttpMethod method, string pathPattern, Func<RomMRequest, RawClientResponse?> responder) =>
            _rawRules.Add(new RawRule(method, pathPattern, responder));

        private JsonDocument? HandleJson(HttpMethod method, string path, bool publicEndpoint, object? body)
        {
            var request = new RomMRequest(method, path, publicEndpoint, body);
            Requests.Add(request);

            var rule = _jsonRules.FirstOrDefault(r => r.Method == method && Matches(r.PathPattern, path));
            if (rule == null)
            {
                throw new InvalidOperationException(
                    $"FakeRomMServer has no response configured for {method} {path}. " +
                    $"Call server.RespondTo(HttpMethod.{method.Method[0]}{method.Method[1..].ToLower()}, " +
                    $"\"{PathOnly(path)}\", ...) before invoking the code under test.");
            }

            return rule.Responder(request);
        }

        private RawClientResponse? HandleRaw(HttpMethod method, string path, bool publicEndpoint, object? body)
        {
            var request = new RomMRequest(method, path, publicEndpoint, body);
            Requests.Add(request);

            var rule = _rawRules.FirstOrDefault(r => r.Method == method && Matches(r.PathPattern, path));
            if (rule == null)
            {
                throw new InvalidOperationException(
                    $"FakeRomMServer has no raw response configured for {method} {path}. " +
                    $"Call server.RespondToRaw(HttpMethod.{method.Method[0]}{method.Method[1..].ToLower()}, " +
                    $"\"{PathOnly(path)}\", ...) before invoking the code under test.");
            }

            return rule.Responder(request);
        }

        private static string PathOnly(string path) => path.Split('?', 2)[0];

        private static bool Matches(string pattern, string path)
        {
            var pathOnly = PathOnly(path);

            if (!pattern.Contains('*'))
                return pattern == pathOnly || pattern == path;

            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(pathOnly, regexPattern) || Regex.IsMatch(path, regexPattern);
        }
    }

}


