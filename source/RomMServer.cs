using Graviton.Models.Notifications;

using Playnite;

using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Graviton
{
    public class RawClientResponse
    {
        public HttpStatusCode? Status = null;
        public HttpContent? Content = null;
    }

    public static class RomMServer
    {
        private static HttpClient httpClient = new HttpClient();

        private static GravitonPlugin? _plugin;
        private static bool IsInitialized = false;

        private static string Host => _plugin?.Settings.Host ?? "";

        static RomMServer()
        {
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            httpClient.Timeout = TimeSpan.FromSeconds(30); // Make user editable
        }

        public static void Initialize(GravitonPlugin plugin)
        {
            _plugin = plugin;
            IsInitialized = true;
        }

        public static void AddHeader(string name, string value)
        {
            if (!httpClient.DefaultRequestHeaders.Contains(name))
                httpClient.DefaultRequestHeaders.Add(name, value);
        }
        public static void RemoveHeader(string name)
        {
            if (httpClient.DefaultRequestHeaders.Contains(name))
                httpClient.DefaultRequestHeaders.Remove(name);
        }

        public static void ConfigureBasicAuth(string username, string password)
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("RomMServer hasn't been initialized cannot perform HTTP requests!!");
                return;
            }

            httpClient.DefaultRequestHeaders.Authorization = null;
            var base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
            foreach (var header in _plugin!.Settings.CustomHeaders.Where(x => x.Enabled))
            {
                httpClient.DefaultRequestHeaders.Remove(header.Name);
                httpClient.DefaultRequestHeaders.Add(header.Name, header.Value);
            }
        }
        public static void ConfigureClientToken(string clientToken)
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("RomMServer hasn't been initialized cannot perform HTTP requests!!");
                return;
            }

            httpClient.DefaultRequestHeaders.Authorization = null;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
            foreach (var header in _plugin!.Settings.CustomHeaders.Where(x => x.Enabled))
            {
                httpClient.DefaultRequestHeaders.Remove(header.Name);
                httpClient.DefaultRequestHeaders.Add(header.Name, header.Value);
            }
        }

        private static async Task<JsonDocument?> ExecuteAsync(string apiPath, bool PublicEndpoint, Func < Task<HttpResponseMessage>> send, string nofiyType, string locFailedMessage)
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("RomMServer hasn't been initialized cannot perform HTTP requests!!");
                return null;
            }

            if (_plugin!.Settings.AccountState.LastAuthenticated == null && !PublicEndpoint)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                _plugin.Settings.AccountState.User = "----";
                _plugin.Settings.AccountState.UserType = "----";
                _plugin.Settings.AccountState.LastAuthenticated = null;
                return null;
            }

            HttpResponseMessage? response = null;
            Stream? content = null;
            try
            {
                response = await send();
                content = await response.Content.ReadAsStreamAsync();
                response.EnsureSuccessStatusCode();

                if (content.Length <= 0)
                    return null;

                _plugin.Settings.AccountState.AuthenticateFailed = HttpStatusCode.OK;
                return await JsonDocument.ParseAsync(content);
            }
            catch (Exception ex)
            {
                if (response == null || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _plugin.Settings.AccountState.AuthenticateFailed = response?.StatusCode;

                    if (response?.StatusCode == HttpStatusCode.Unauthorized || response?.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _plugin.Account!.ResetLocalAccountState();
                        _plugin.Settings.AccountState.AuthenticateFailed = response?.StatusCode;
                    }
                }

                if (response != null && (int)response.StatusCode > 399 && (int)response.StatusCode < 500 && content?.Length > 0)
                {
                    var body = new StreamReader(content!, Encoding.UTF8).ReadToEnd();
                    var displayMessage = ExtractErrorResponse(body);

                    GravitonNotify.Add(new GravitonNotification("graviton.request.4xx", Loc.GetString("ServerResponded", ("Message", displayMessage)), GravitonSeverity.Error));
                    GravitonPlugin.Logger.Error($"Path: {apiPath}\nRaw Details: {body}");
                }
                else
                {
                    GravitonNotify.Add(new GravitonNotification(nofiyType, $"{Loc.GetString(locFailedMessage, [("APIPath", apiPath)])} - {ex.Message}", GravitonSeverity.Error, ex));
                }

                return null;
            }
        }

        private static async Task<RawClientResponse?> ExecuteRawAsync(string apiPath, bool PublicEndpoint, Func<Task<HttpResponseMessage>> send, string nofiyType, string locFailedMessage)
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("HttpClientSingleton hasn't been initialized cannot perform HTTP requests!!");
                return null;
            }

            if (_plugin!.Settings.AccountState.LastAuthenticated == null && !PublicEndpoint)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                _plugin.Settings.AccountState.User = "----";
                _plugin.Settings.AccountState.UserType = "----";
                _plugin.Settings.AccountState.LastAuthenticated = null;
                return null;
            }

            HttpResponseMessage? response = null;
            Stream? content = null;
            try
            {
                response = await send();
                response.EnsureSuccessStatusCode();


                _plugin.Settings.AccountState.AuthenticateFailed = HttpStatusCode.OK;
                return new() { Status = HttpStatusCode.OK, Content = response.Content };
            }
            catch (Exception ex)
            {
                if (response == null || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _plugin.Settings.AccountState.AuthenticateFailed = response?.StatusCode;

                    if (response?.StatusCode == HttpStatusCode.Unauthorized || response?.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _plugin.Account!.ResetLocalAccountState();
                        _plugin.Settings.AccountState.AuthenticateFailed = response?.StatusCode;
                    }
                }

                if (response != null && (int)response.StatusCode > 399 && (int)response.StatusCode < 500 && content?.Length > 0)
                {
                    var body = new StreamReader(content!, Encoding.UTF8).ReadToEnd();
                    var displayMessage = ExtractErrorResponse(body);

                    GravitonNotify.Add(new GravitonNotification("graviton.request.4xx", Loc.GetString("ServerResponded", ("Message", displayMessage)), GravitonSeverity.Error));
                    GravitonPlugin.Logger.Error($"Path: {apiPath}\nRaw Details: {body}");
                }
                else
                {
                    GravitonNotify.Add(new GravitonNotification(nofiyType, $"{Loc.GetString(locFailedMessage, [("APIPath", apiPath)])} - {ex.Message}", GravitonSeverity.Error, ex));
                }

                return new() { Status = response?.StatusCode, Content = null };
            }
        }

        public static Task<JsonDocument?> GETAsync(string APIPath, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.GetAsync($"{Host}{APIPath}"), "graviton.GET.failed", "GETFailed");
        public static Task<JsonDocument?> POSTAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.PostAsync($"{Host}{APIPath}", content), "graviton.POST.failed", "POSTFailed");
        public static Task<JsonDocument?> POSTAsync(string APIPath, object json, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.PostAsJsonAsync($"{Host}{APIPath}", json), "graviton.POST.failed", "POSTFailed");
        public static Task<JsonDocument?> PUTAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.PutAsync($"{Host}{APIPath}", content), "graviton.PUT.failed", "PUTFailed");
        public static Task<JsonDocument?> PUTAsync(string APIPath, object json, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.PutAsJsonAsync($"{Host}{APIPath}", json), "graviton.PUT.failed", "PUTFailed");
        public static Task<JsonDocument?> DELETEAsync(string APIPath, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.DeleteAsync($"{Host}{APIPath}"), "graviton.DELETE.failed", "DELETEFailed");


        public static Task<RawClientResponse?> RawGETAsync(string APIPath, bool PublicEndpoint = false) => ExecuteRawAsync(APIPath, PublicEndpoint, () => httpClient.GetAsync($"{Host}{APIPath}"), "graviton.GET.failed", "GETFailed");
        public static Task<RawClientResponse?> RawPOSTAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) => ExecuteRawAsync(APIPath, PublicEndpoint, () => httpClient.PostAsync($"{Host}{APIPath}", content), "graviton.POST.failed", "POSTFailed");
        public static Task<RawClientResponse?> RawPOSTAsync(string APIPath, object json, bool PublicEndpoint = false) => ExecuteRawAsync(APIPath, PublicEndpoint, () => httpClient.PostAsJsonAsync($"{Host}{APIPath}", json), "graviton.POST.failed", "POSTFailed");
        public static Task<RawClientResponse?> RawPUTAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) => ExecuteRawAsync(APIPath, PublicEndpoint, () => httpClient.PutAsync($"{Host}{APIPath}", content), "graviton.PUT.failed", "PUTFailed");
        public static Task<RawClientResponse?> RawPUTAsync(string APIPath, object json, bool PublicEndpoint = false) => ExecuteRawAsync(APIPath, PublicEndpoint, () => httpClient.PutAsJsonAsync($"{Host}{APIPath}", json), "graviton.PUT.failed", "PUTFailed");


        private static string ExtractErrorResponse(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);

                if (!doc.RootElement.TryGetProperty("detail", out var detail))
                    return body;

                if (detail.ValueKind == JsonValueKind.String)
                    return detail.GetString() ?? body;

                if (detail.ValueKind == JsonValueKind.Array)
                {
                    var messages = new List<string>();
                    foreach (var item in detail.EnumerateArray())
                    {
                        string field = "field";
                        if (item.TryGetProperty("loc", out var loc) && loc.ValueKind == JsonValueKind.Array)
                        {
                            field = string.Join(".", loc.EnumerateArray().Select(x =>
                                x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString()));
                        }

                        string msg = item.TryGetProperty("msg", out var m) ? (m.GetString() ?? "invalid") : "invalid";
                        messages.Add($"{field}: {msg}");
                    }

                    return messages.Count > 0 ? string.Join("; ", messages) : body;
                }

                return body;
            }
            catch (JsonException)
            {
                return body;
            }
        }
    }
}
