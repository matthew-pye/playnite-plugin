
using Playnite;

using RomMLibrary.Models.RomM;
using RomMLibrary.Models.RomM.Collection;
using RomMLibrary.Models.RomM.Rom;
using RomMLibrary.Settings;

using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace RomMLibrary.Status
{
    public class StatusController
    {
        RomMLibraryPlugin Plugin;
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly IPlayniteApi PlayniteApi;

        public StatusController(RomMLibraryPlugin plugin) 
        {
            Plugin = plugin;
            PlayniteApi = RomMLibraryPlugin.PlayniteApi ?? throw new Exception("Playnite API is null, cannot continue!");
        }

        // Syncing
        public bool SyncUserData(ref RomMLibraryPluginSettings settings)
        {
            try
            {
                // Check server is present
                HttpResponseMessage response = HttpClientSingleton.Instance.GetAsync($"{settings.Host}/api/heartbeat", HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken()).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                Stream body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();

                using (StreamReader reader = new StreamReader(body))
                {
                    var jsonResponse = JsonDocument.Parse(reader.ReadToEnd());
                    ServerInfo info = jsonResponse.RootElement.GetProperty("SYSTEM").Deserialize<ServerInfo>();

                    settings.ServerVersion = info.Version;
                }

                // Get user info
                response = HttpClientSingleton.Instance.GetAsync($"{settings.Host}/api/users/me", System.Net.Http.HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken()).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                RomMUser userinfo;

                using (StreamReader reader = new StreamReader(body))
                {
                    var jsonResponse = JsonDocument.Parse(reader.ReadToEnd());
                    userinfo = jsonResponse.RootElement.Deserialize<RomMUser>() ?? throw new Exception("Failed to deserialize UserInfo!");
                }

                if (!string.IsNullOrEmpty(userinfo.IconPath))
                {
                    response = HttpClientSingleton.Instance.GetAsync($"{settings.Host}/api/raw/assets/{userinfo.IconPath}", System.Net.Http.HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken()).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    var imagebytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    File.WriteAllBytes($"{PlayniteApi.UserDataDir}\\avatar.png", imagebytes);
                    settings.ProfilePath = $"{PlayniteApi.UserDataDir}\\avatar.png";
                }
                else
                {
                    settings.ProfilePath = Path.Combine(Plugin.PluginDLLPath, @"profile.png");
                }

                settings.UserType = userinfo.Role;
                settings.User = userinfo.Username;
                settings.UserID = userinfo.Id;
                settings.ConnectionFailed = false;
                settings.ConnectionSuccess = true;

            }
            catch (Exception ex)
            {
                settings.ConnectionFailed = true;
                settings.ConnectionSuccess = false;
                settings.ProfilePath = Path.Combine(Plugin.PluginDLLPath, @"profile.png");
                settings.User = "----";
                settings.UserType = "----";
                settings.ServerVersion = "---";
                LogManager.GetLogger().Error($"[Test Connection] Failed to read response! {ex}");
                PlayniteApi.Notifications.Add(new NotificationMessage(RomMLibraryPlugin.Id, $"{Loc.GetString("ServerPollFailed")}: {ex.Message}", NotificationSeverity.Error));
                return false;
            }

            return true;
        }
        public bool RegisterDevice(ref RomMLibraryPluginSettings settings)
        {
            try
            {
                HttpResponseMessage response;
                Stream body;

                // Check to see if device ID is still avaiable 
                if (!string.IsNullOrEmpty(settings.DeviceID))
                {
                    response = HttpClientSingleton.Instance.GetAsync($"{settings.Host}/api/devices", System.Net.Http.HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken()).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();

                    body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                    List<RomMDevice> devices;

                    using (StreamReader reader = new StreamReader(body))
                    {
                        var jsonResponse = JsonDocument.Parse(reader.ReadToEnd());
                        devices = jsonResponse.RootElement.Deserialize<List<RomMDevice>>() ?? throw new Exception("Unable to deserialize UserInfo!");
                    }

                    if (devices.Any(x => x.ID == settings.DeviceID))
                        return true;
                }

                // Setup data for new device to be added to RomM
                RomMRegisterDevice newDevice = new();
                newDevice.Platform = "Windows";
                newDevice.Client = "Playnite Plugin";
                newDevice.ClientVersion = RomMLibraryPlugin.Version.ToString();
                newDevice.MACAddress = (from nic in NetworkInterface.GetAllNetworkInterfaces()
                                        where nic.OperationalStatus == OperationalStatus.Up
                                        select nic.GetPhysicalAddress().ToString()
                                        ).FirstOrDefault();
                newDevice.HostName = Environment.MachineName;

                // Register device with RomM
                response = HttpClientSingleton.Instance.PostAsJsonAsync($"{settings.Host}/api/devices", newDevice, new System.Threading.CancellationToken()).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                RomMRegisterDeviceResponse newRomMDevice;

                using (StreamReader reader = new StreamReader(body))
                {
                    var jsonResponse = JsonDocument.Parse(reader.ReadToEnd());
                    newRomMDevice = jsonResponse.RootElement.Deserialize<RomMRegisterDeviceResponse>() ?? throw new Exception("Unable to deserialize register device response!");
                }

                // Set ID that RomM responds with
                settings.DeviceID = newRomMDevice.DeviceID ?? throw new Exception("Response Device ID is null!");
                return true;

            }
            catch (Exception ex)
            {
                settings.ConnectionFailed = true;
                settings.ConnectionSuccess = false;
                settings.ProfilePath = Path.Combine(Plugin.PluginDLLPath, @"profile.png");
                settings.User = "----";
                settings.UserType = "----";
                settings.ServerVersion = "---";
                LogManager.GetLogger().Error($"[Register Device] Failed to read response! {ex}");
                PlayniteApi.Notifications.Add(new NotificationMessage(RomMLibraryPlugin.Id, $"{Loc.GetString("ServerPollFailed")}: {ex.Message}", NotificationSeverity.Error));
                return false;
            }
        }

        // Favourites
        private RomMCollection? CreateFavorites()
        {
            string apiCollectionUrl = $"{Plugin.Settings.Host}/api/collections?is_favorite=true&is_public=false";
            try
            {
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent("Favorites"), "name");

                HttpResponseMessage postResponse = HttpClientSingleton.Instance.PostAsync(apiCollectionUrl, formData).GetAwaiter().GetResult();
                postResponse.EnsureSuccessStatusCode();

                string body = postResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonSerializer.Deserialize<RomMCollection>(body);
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"Request exception: {e.Message}");
                return null;
            }
        }

        public RomMCollection? PullFavourites()
        {
            string apiFavoriteUrl = $"{Plugin.Settings.Host}/api/collections";
            try
            {
                // Make the request and get the response
                HttpResponseMessage response = HttpClientSingleton.Instance.GetAsync(apiFavoriteUrl).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                // Assuming the response is in JSON format
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                RomMCollection? favourites = JsonSerializer.Deserialize<List<RomMCollection>>(body)?.First(x => x.Name == "Favorites");

                if (favourites == null)
                    return CreateFavorites();

                return favourites;
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"Request exception: {e.Message}");
                return null;
            }
        }

        public void UpdateFavorites(RomMCollection favoriteCollection, List<int> romMRomIDs)
        {
            if (favoriteCollection == null)
            {
                Logger.Error($"Can't update favorites, collection is null");
                return;
            }

            string apiCollectionUrl = $"{Plugin.Settings.Host}/api/collections";
            try
            {
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(JsonSerializer.Serialize(romMRomIDs)), "rom_ids");
                HttpResponseMessage putResponse = HttpClientSingleton.Instance.PutAsync($"{apiCollectionUrl}/{favoriteCollection.Id}", formData).GetAwaiter().GetResult();
                putResponse.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"Request exception: {e.Message}");
            }
        }

        // Play Status
        public CompletionStatus? DetermineCompletionStatus(RomMRom ROM)
        {
            if(ROM.RomUser?.Status != null)
            {
                var status = RomMRomUser.CompletionStatusMap[ROM.RomUser.Status];
                return PlayniteApi.Library.CompletionStatuses.First(x => x.Name == status);
            }

            return null;
        }
        public CompletionStatus? DetermineCompletionStatus(Game game)
        {
            if (game.CompletionStatusId != null)
            {
                return PlayniteApi.Library.CompletionStatuses.First(x => x.Id == game.CompletionStatusId);
            }

            return null;
        }

        public void UpdateStatus(Game game)
        {
            try
            {
                if (game.CompletionStatusId == null) return;

                int romMID;
                if (int.TryParse(game.LibraryGameId?.Split(':')[0], out romMID))
                {
                    Logger.Error("Failed to parse GameID, Skipping status update!");
                    return;
                }

                var status = PlayniteApi.Library.CompletionStatuses.Get(game.CompletionStatusId)?.Name;
                var updatePayload = new
                {
                    data = new
                    {
                        backlogged = status == "Plan to Play",
                        now_playing = status == "Playing",
                        status = RomMRomUser.CompletionStatusMap.FirstOrDefault((kv) => kv.Value == status && kv.Value != "Playing" && kv.Value != "Plan to Play" && kv.Value != "Not Played").Key
                    }
                };
                string apiRomMRomUserProps = $"{Plugin.Settings.Host}api/roms/{romMID}/props";
                HttpResponseMessage response = HttpClientSingleton.Instance.PutAsync(apiRomMRomUserProps, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"RomM Status Sync Failed for {game.Name}");
            }
        }
    }
}
