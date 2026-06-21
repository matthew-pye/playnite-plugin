using Graviton.Models.Notifications;

using Playnite;

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
        private static GravitonPlugin _plugin { get => GravitonPlugin.Instance; } 

        private static readonly HttpClient httpClient = new HttpClient();
        public static HttpClient Instance => httpClient;

        static HttpClientSingleton()
        {
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            httpClient.Timeout = TimeSpan.FromSeconds(30); // Make user editable
        }

        public static void ConfigureBasicAuth(string username, string password)
        {
            Instance.DefaultRequestHeaders.Authorization = null;
            var base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            Instance.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
        }
        public static void ConfigureClientToken(string clientToken)
        {
            Instance.DefaultRequestHeaders.Authorization = null;
            Instance.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        }


        public static async Task<JsonDocument?> RomMGetAsync(string APIPath)
        {
            if (_plugin.Settings.LastAuthenticated == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                return null;
            }

            HttpResponseMessage? response = null;
            try
            {
                response = await httpClient.GetAsync($"{_plugin.Settings.Host}{APIPath}");
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                return await JsonDocument.ParseAsync(stream);
            }
            catch (Exception ex)
            {
                if (response != null && (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden))
                    GravitonPlugin.Instance.Settings.LastAuthenticated = null;

                GravitonNotify.Add(new GravitonNotification("graviton.GET.failed", $"{Loc.GetString("GETFailed")} {APIPath} - {ex.Message}", GravitonSeverity.Error, ex));
                return null;
            }

        }

        public static async Task<JsonDocument?> RomMPostJsonAsync(string APIPath, object JSON)
        {
            if (_plugin.Settings.LastAuthenticated == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                return null;
            }

            HttpResponseMessage? response = null;
            try
            {
                response = await httpClient.PostAsJsonAsync($"{_plugin.Settings.Host}{APIPath}", JSON);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                return await JsonDocument.ParseAsync(stream);
            }
            catch (Exception ex)
            {
                if (response != null && (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden))
                    GravitonPlugin.Instance.Settings.LastAuthenticated = null;

                GravitonNotify.Add(new GravitonNotification("graviton.POST.failed", $"{Loc.GetString("POSTFailed")} {APIPath} - {ex.Message}", GravitonSeverity.Error, ex));
                return null;
            }
        }

        public static async Task<JsonDocument?> RomMPutJsonAsync(string APIPath, object JSON)
        {
            if (_plugin.Settings.LastAuthenticated == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                return null;
            }

            HttpResponseMessage? response = null;
            try
            {
                response = await httpClient.PutAsJsonAsync($"{_plugin.Settings.Host}{APIPath}", JSON);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                return await JsonDocument.ParseAsync(stream);
            }
            catch (Exception ex)
            {
                if (response != null && (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden))
                    GravitonPlugin.Instance.Settings.LastAuthenticated = null;

                GravitonNotify.Add(new GravitonNotification("graviton.PUT.failed", $"{Loc.GetString("PUTFailed")} {APIPath} - {ex.Message}", GravitonSeverity.Error, ex));
                return null;
            }
        }

        public static async Task<JsonDocument?> RomMPostContentAsync(string APIPath, HttpContent Content)
        {
            if (_plugin.Settings.LastAuthenticated == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                return null;
            }

            HttpResponseMessage? response = null;
            try
            {
                response = await httpClient.PostAsync($"{_plugin.Settings.Host}{APIPath}", Content);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                return await JsonDocument.ParseAsync(stream);
            }
            catch (Exception ex)
            {
                if (response != null && (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden))
                    GravitonPlugin.Instance.Settings.LastAuthenticated = null;

                GravitonNotify.Add(new GravitonNotification("graviton.POST.failed", $"{Loc.GetString("POSTFailed")} {APIPath} - {ex.Message}", GravitonSeverity.Error, ex));
                return null;
            }
        }

        public static async Task<JsonDocument?> RomMPutContentAsync(string APIPath, HttpContent Content)
        {
            if (_plugin.Settings.LastAuthenticated == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                return null;
            }

            HttpResponseMessage? response = null;
            try
            {
                response = await httpClient.PutAsync($"{_plugin.Settings.Host}{APIPath}", Content);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                return await JsonDocument.ParseAsync(stream);
            }
            catch (Exception ex)
            {
                if (response != null && (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden))
                    GravitonPlugin.Instance.Settings.LastAuthenticated = null;

                GravitonNotify.Add(new GravitonNotification("graviton.PUT.failed", $"{Loc.GetString("PUTFailed")} {APIPath} - {ex.Message}", GravitonSeverity.Error, ex));
                return null;
            }
        }
    }
}
