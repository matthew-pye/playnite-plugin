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
    public static class HttpClientSingleton
    {
        public static HttpClient Instance => httpClient;

        private static readonly HttpClient httpClient = new HttpClient();

        private static GravitonPlugin? _plugin;
        private static bool IsInitialized = false;

        private static string Host => _plugin!.Settings.Host;

        static HttpClientSingleton()
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

        public static void ConfigureBasicAuth(string username, string password)
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("HttpClientSingleton hasn't been initialized cannot perform HTTP requests!!");
                return;
            }

            Instance.DefaultRequestHeaders.Authorization = null;
            var base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            Instance.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
            foreach (var header in _plugin!.Settings.CustomHeaders.Where(x => x.Enabled))
            {
                Instance.DefaultRequestHeaders.Remove(header.Name);
                Instance.DefaultRequestHeaders.Add(header.Name, header.Value);
            }
        }
        public static void ConfigureClientToken(string clientToken)
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("HttpClientSingleton hasn't been initialized cannot perform HTTP requests!!");
                return;
            }

            Instance.DefaultRequestHeaders.Authorization = null;
            Instance.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
            foreach (var header in _plugin!.Settings.CustomHeaders.Where(x => x.Enabled))
            {
                Instance.DefaultRequestHeaders.Remove(header.Name);
                Instance.DefaultRequestHeaders.Add(header.Name, header.Value);
            }
        }

        private static async Task<JsonDocument?> ExecuteAsync(string apiPath, bool PublicEndpoint, Func < Task<HttpResponseMessage>> send, string nofiyType, string locFailedMessage)
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("HttpClientSingleton hasn't been initialized cannot perform HTTP requests!!");
                return null;
            }

            if (_plugin!.Settings.AccountState.LastAuthenticated == null && !PublicEndpoint)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
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

                return await JsonDocument.ParseAsync(content);
            }
            catch (Exception ex)
            {
                if (response?.StatusCode == HttpStatusCode.Unauthorized || response?.StatusCode == HttpStatusCode.Forbidden)
                    _plugin.Settings.AccountState.LastAuthenticated = null;

                GravitonNotify.Add(new GravitonNotification(nofiyType, $"{Loc.GetString(locFailedMessage, [("APIPath", apiPath)])} - {ex.Message}", GravitonSeverity.Error, ex));

                if (response?.StatusCode == HttpStatusCode.UnprocessableContent && content?.Length <= 0)
                    GravitonPlugin.Logger.Error(new StreamReader(content!, Encoding.UTF8).ReadToEnd());

                return null;
            }
        }

        public static Task<JsonDocument?> RomMGetAsync(string APIPath, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.GetAsync($"{Host}{APIPath}"), "graviton.GET.failed", "GETFailed");
        public static Task<JsonDocument?> RomMDeleteAsync(string APIPath, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.DeleteAsync($"{Host}{APIPath}"), "graviton.DELETE.failed", "DELETEFailed");

        public static Task<JsonDocument?> RomMPostJsonAsync(string APIPath, object json, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.PostAsJsonAsync($"{Host}{APIPath}", json), "graviton.POST.failed", "POSTFailed");
        public static Task<JsonDocument?> RomMPutJsonAsync(string APIPath, object json, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.PutAsJsonAsync($"{Host}{APIPath}", json), "graviton.PUT.failed", "PUTFailed");

        public static Task<JsonDocument?> RomMPostContentAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.PostAsync($"{Host}{APIPath}", content), "graviton.POST.failed", "POSTFailed");
        public static Task<JsonDocument?> RomMPutContentAsync(string APIPath, HttpContent content, bool PublicEndpoint = false) => ExecuteAsync(APIPath, PublicEndpoint, () => httpClient.PutAsync($"{Host}{APIPath}", content), "graviton.PUT.failed", "PUTFailed");
    }
}
