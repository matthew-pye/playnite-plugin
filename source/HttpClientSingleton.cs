using Graviton.Models.Notifications;
using Graviton.Settings;

using Playnite;
using Playnite.WebViews;

using System.CodeDom;
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
        }

        public static void ConfigureBasicAuth(string username, string password)
        {
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            Instance.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
        }
        public static void ConfigureClientToken(string clientToken)
        {
            Instance.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        }

        public static async Task<JsonDocument?> RomMGetAsync(string APIPath)
        {
            if (_plugin.Settings.LastAuthenticated == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                return null;
            }
            HttpResponseMessage response = await httpClient.GetAsync($"{_plugin.Settings.Host}{APIPath}", new System.Threading.CancellationToken());

            try
            { 
                response = response.EnsureSuccessStatusCode();

                Stream body = await response.Content.ReadAsStreamAsync();
                using (StreamReader reader = new StreamReader(body))
                {
                    return JsonDocument.Parse(reader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
                    GravitonPlugin.Instance.Settings.LastAuthenticated = null;

                GravitonNotify.Add(new GravitonNotification("graviton.GET.failed", $"{Loc.GetString("GETFailed")} {APIPath} - {ex.Message}", GravitonSeverity.Error));
                return null;
            }
        }

        public static async Task<JsonDocument?> RomMPostWithJsonAsync(string APIPath, object JSON)
        {
            if (_plugin.Settings.LastAuthenticated == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                return null;
            }

            HttpResponseMessage response = await httpClient.PostAsJsonAsync($"{_plugin.Settings.Host}{APIPath}", JSON, new System.Threading.CancellationToken());
            try
            {
                response = response.EnsureSuccessStatusCode();

                Stream body = await response.Content.ReadAsStreamAsync();
                using (StreamReader reader = new StreamReader(body))
                {
                    return JsonDocument.Parse(reader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
                    GravitonPlugin.Instance.Settings.LastAuthenticated = null;

                GravitonNotify.Add(new GravitonNotification("graviton.POST.failed", $"{Loc.GetString("POSTFailed")} {APIPath} - {ex.Message}", GravitonSeverity.Error));
                return null;
            }
        }

        public static async Task<JsonDocument?> RomMPutWithJsonAsync(string APIPath, object JSON)
        {
            if (_plugin.Settings.LastAuthenticated == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.authenticated.failed", Loc.GetString("Reauthenticate"), GravitonSeverity.Error));
                return null;
            }
                
            HttpResponseMessage response = await httpClient.PutAsJsonAsync($"{_plugin.Settings.Host}{APIPath}", JSON, new System.Threading.CancellationToken());
            try
            {
                response = response.EnsureSuccessStatusCode();

                Stream body = await response.Content.ReadAsStreamAsync();
                using (StreamReader reader = new StreamReader(body))
                {
                    return JsonDocument.Parse(reader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
                    GravitonPlugin.Instance.Settings.LastAuthenticated = null;

                GravitonNotify.Add(new GravitonNotification("graviton.PUT.failed", $"{Loc.GetString("PUTFailed")} {APIPath} - {ex.Message}", GravitonSeverity.Error));
                return null;
            }
        }

    }
}
