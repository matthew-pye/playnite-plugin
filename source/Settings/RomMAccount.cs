using Graviton.Models.Notifications;
using Graviton.Models.RomM;

using Playnite;

using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace Graviton.Settings
{
    internal class RomMAccount
    {

        private GravitonPlugin _plugin {get => GravitonPlugin.Instance ?? throw new Exception("Plugin is null, cannot continue"); }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi ?? throw new Exception("PlayniteAPI is null, cannot continue"); }

        public async Task<ServerInfo?> Heartbeat(GravitonPluginSettings settings)
        {
            try
            {
                HttpResponseMessage response = await HttpClientSingleton.Instance.GetAsync($"{settings.Host}/api/heartbeat", new System.Threading.CancellationToken());
                response = response.EnsureSuccessStatusCode();

                Stream body = await response.Content.ReadAsStreamAsync();
                using (StreamReader reader = new StreamReader(body))
                {
                    settings.LastAuthenticated = DateTime.UtcNow;
                    return JsonDocument.Parse(reader.ReadToEnd()).RootElement.GetProperty("SYSTEM").Deserialize<ServerInfo>();
                }
            }
            catch (Exception ex)
            {
                settings.LastAuthenticated = null;
                GravitonNotify.Add(new GravitonNotification("graviton.heartbeat.failed", $"Failed to ping server - {ex.Message}", GravitonSeverity.Error));
                SyncFailed(settings);
                return null;
            }
        }

        public async Task<bool> Login(GravitonPluginSettings settings)
        {
            // Check Host and Client token/UsernamePassword are set!
            if (string.IsNullOrEmpty(settings.Host))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.login.host.notset", $"Cannot login - host is not set!", GravitonSeverity.Error));
                SyncFailed(settings);
                return false;
            }

            if (settings.UseBasicAuth)
            {
                if (string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(settings.Password))
                {
                    _playniteAPI.Notifications.Add(new NotificationMessage("graviton.login.userorpass.notset", "Login failed - Username or Password not set!", NotificationSeverity.Error));
                    GravitonPlugin.Logger?.Error("");
                }
                    
                HttpClientSingleton.ConfigureBasicAuth(settings.Username, settings.Password);
            }
            else
            {
                if (string.IsNullOrEmpty(settings.ClientToken))
                {
                    _playniteAPI.Notifications.Add(new NotificationMessage("graviton.login.token.notset", "Login failed - Client Token not set!", NotificationSeverity.Error));
                    GravitonPlugin.Logger?.Error("");
                    SyncFailed(settings);
                    return false;
                }
                    

                HttpClientSingleton.ConfigureClientToken(settings.ClientToken);
            }

            settings.LastAuthenticated = DateTime.UtcNow;

            ServerInfo? heartbeat = await Heartbeat(settings);
            if (heartbeat == null)
            {
                SyncFailed(settings); 
                return false;
            }
                
            settings.ServerVersion = heartbeat.Value.Version;


            if (!(await RegisterNewDevice(settings)))
            {
                SyncFailed(settings);
                return false;
            }
            
            else if(!(await UpdateDevice(settings)))
            {
                SyncFailed(settings);
                return false;
            }

            if (!(await SyncUserData(settings)))
            {
                SyncFailed(settings);
                return false;
            }

            GravitonNotify.Add(new GravitonNotification("graviton.Account.loggedin", $"Login successful", GravitonSeverity.Success));
            return true;
        }

        async Task<bool> SyncUserData(GravitonPluginSettings settings)
        {
            var result = await HttpClientSingleton.RomMGetAsync("/api/users/me");
            if (result == null)
                return false;

            var userinfo = result.RootElement.Deserialize<RomMUser>() ?? throw new Exception("Failed to deserialize UserInfo!");

            try
            {
                if (!string.IsNullOrEmpty(userinfo.IconPath))
                {
                    var response = await HttpClientSingleton.Instance.GetAsync($"{settings.Host}/api/raw/assets/{userinfo.IconPath}", System.Net.Http.HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken());
                    response.EnsureSuccessStatusCode();
                    var imagebytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    File.WriteAllBytes($"{GravitonPlugin.PlayniteApi?.UserDataDir}\\avatar.png", imagebytes);
                    settings.ProfilePath = $"{GravitonPlugin.PlayniteApi?.UserDataDir}\\avatar.png";
                }
                else
                {
                    settings.ProfilePath = Path.Combine(_plugin.PluginDLLPath, @"profile.png");
                }
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.GET.profileicon.failed", $"Failed to get profile icon from server - {ex.Message}", GravitonSeverity.Error));
            }
            

            settings.UserType = userinfo.Role;
            settings.User = userinfo.Username;
            settings.UserID = userinfo.Id;
            return true;

        }

        async Task<bool> RegisterNewDevice(GravitonPluginSettings settings)
        {
            // Check to see if current device id is valid
            if (!string.IsNullOrEmpty(settings.DeviceID))
            {
                var result = await HttpClientSingleton.RomMGetAsync("/api/devices");
                if (result != null)
                {
                    try
                    {
                        List<RomMDevice> devices = result.RootElement.Deserialize<List<RomMDevice>>() ?? throw new Exception("Unable to deserialize UserInfo!");
                        if (devices.Any(x => x.ID == settings.DeviceID))
                            return true;
                    }
                    catch (Exception ex)
                    {
                        GravitonNotify.Add(new GravitonNotification("graviton.GET.device.failed", $"Failed to GET devices - {ex.Message}", GravitonSeverity.Warn));
                        return false;
                    }
                }
                
            }

            // Setup data for new device to be added to RomM
            RomMRegisterDevice newDevice = new();
            newDevice.Name = $"Graviton-{Environment.MachineName}";
            newDevice.Platform = "Windows";
            newDevice.Client = "Graviton (Playnite Plugin)";
            newDevice.ClientVersion = GravitonPlugin.Version.ToString();
            newDevice.MACAddress = (from nic in NetworkInterface.GetAllNetworkInterfaces() where nic.OperationalStatus == OperationalStatus.Up select nic.GetPhysicalAddress().ToString()).FirstOrDefault();
            newDevice.HostName = Environment.MachineName;

            var request = await HttpClientSingleton.RomMPostWithJsonAsync("/api/devices", newDevice);
            if (request == null)
                return false;

            try
            {
                RomMRegisterDeviceResponse newRomMDevice = request.RootElement.Deserialize<RomMRegisterDeviceResponse>() ?? throw new Exception("Unable to deserialize register device response!");

                // Set ID that RomM responds with
                settings.DeviceID = newRomMDevice.DeviceID ?? throw new Exception("Response Device ID is null!");
                return true;
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.POST.device.failed", $"Failed to create new device - {ex.Message}", GravitonSeverity.Error));
                return false;
            }
        }

        async Task<bool> UpdateDevice(GravitonPluginSettings settings)
        {
            // Rebuild device data
            RomMRegisterDevice newDevice = new();
            newDevice.Platform = "Windows";
            newDevice.Client = "Graviton (Playnite Plugin)";
            newDevice.ClientVersion = GravitonPlugin.Version.ToString();
            newDevice.MACAddress = (from nic in NetworkInterface.GetAllNetworkInterfaces() where nic.OperationalStatus == OperationalStatus.Up select nic.GetPhysicalAddress().ToString()).FirstOrDefault();
            newDevice.HostName = Environment.MachineName;

            var result = HttpClientSingleton.RomMPutWithJsonAsync($"/api/devices/{settings.DeviceID}", newDevice);
            if (result == null)
                return false;

            return true;
        }

        void SyncFailed(GravitonPluginSettings settings)
        {
            settings.ProfilePath = Path.Combine(_plugin.PluginDLLPath, @"profile.png");
            settings.User = "----";
            settings.UserType = "----";
            settings.ServerVersion = "---";
            settings.LastAuthenticated = null;
        }
    }
}
