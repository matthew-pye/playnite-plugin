using Graviton.Models.Notifications;
using Graviton.Models.RomM;

using Playnite;

using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Graviton.Settings
{
    internal class RomMAccount
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        private static readonly Regex _iconPathRegex = new Regex(@"^users/[^/]+/profile/avatar\.(png|jpg|jpeg|webp)$");

        public async Task<ServerInfo?> Heartbeat()
        {
            var result = await HttpClientSingleton.RomMGetAsync("/api/heartbeat");
            if(result == null)
            {
                _plugin.Settings.LastAuthenticated = null;
                GravitonNotify.Add(new GravitonNotification("graviton.heartbeat.failed", Loc.GetString("HeartbeatFailed"), GravitonSeverity.Error));
                SyncFailed();
                return null;
            }

            try
            {
                _plugin.Settings.LastAuthenticated = DateTime.UtcNow;
                return result.RootElement.GetProperty("SYSTEM").Deserialize<ServerInfo>();               
            }
            catch (Exception ex)
            {
                _logger.Error($"Heartbeat failed - {ex}");
                return null;
            }
        }

        public async Task<bool> Login()
        {
            // Check Host and Client token/UsernamePassword are set!
            if (string.IsNullOrEmpty(_plugin.Settings.Host))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.login.host.notset", Loc.GetString("HostNotSet"), GravitonSeverity.Warn));
                SyncFailed();
                return false;
            }

            if (_plugin.Settings.UseBasicAuth)
            {
                if (string.IsNullOrEmpty(_plugin.Settings.UsernameNP) || string.IsNullOrEmpty(_plugin.Settings.PasswordNP))
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.login.userorpass.notset", Loc.GetString("UserPassNotSet"), GravitonSeverity.Warn));
                    SyncFailed();
                    return false;
                }
                 
                HttpClientSingleton.ConfigureBasicAuth(_plugin.Settings.UsernameNP, _plugin.Settings.PasswordNP);
            }
            else
            {
                if (string.IsNullOrEmpty(_plugin.Settings.ClientTokenNP))
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.login.userorpass.notset", Loc.GetString("TokenNotSet"), GravitonSeverity.Warn));
                    SyncFailed();
                    return false;
                }
                    

                HttpClientSingleton.ConfigureClientToken(_plugin.Settings.ClientTokenNP);
            }

            _plugin.Settings.LastAuthenticated = DateTime.UtcNow;

            ServerInfo? heartbeat = await Heartbeat();
            if (heartbeat == null)
            {
                SyncFailed(); 
                return false;
            }
                
            _plugin.Settings.ServerVersion = heartbeat.Value.Version;


            if (!(await RegisterNewDevice()))
            {
                SyncFailed();
                return false;
            }
            
            else if(!(await UpdateDevice()))
            {
                SyncFailed();
                return false;
            }

            if (!(await SyncUserData()))
            {
                SyncFailed();
                return false;
            }

            GravitonNotify.Add(new GravitonNotification("graviton.Account.loggedin", Loc.GetString("LoginSuccess"), GravitonSeverity.Success));
            return true;
        }

        async Task<bool> SyncUserData()
        {
            var result = await HttpClientSingleton.RomMGetAsync("/api/users/me");
            if (result == null)
                return false;

            var userinfo = result.RootElement.Deserialize<RomMUser>() ?? throw new Exception("Failed to deserialize UserInfo!");

            try
            {
                if (!string.IsNullOrEmpty(userinfo.IconPath) && _iconPathRegex.IsMatch(userinfo.IconPath))
                {
                    var response = await HttpClientSingleton.Instance.GetAsync($"{_plugin.Settings.Host}/api/raw/assets/{userinfo.IconPath}", System.Net.Http.HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken());
                    response.EnsureSuccessStatusCode();
                    var imagebytes = await response.Content.ReadAsByteArrayAsync();

                    if (imagebytes.Length > 20 * 1024 * 1024) // 20MB cap
                        throw new Exception("Avatar image exceeds maximum allowed size.");

                    if(string.IsNullOrEmpty(_plugin.PluginDataPath))
                        throw new Exception("Cannot save profile image, PluginData path is unknown!");

                    File.WriteAllBytes($"{_plugin.PluginDataPath}\\avatar.png", imagebytes);
                    _plugin.Settings.ProfilePath = $"{_plugin.PluginDataPath}\\avatar.png";
                }
                else
                {
                    _plugin.Settings.ProfilePath = Path.Combine(_plugin.PluginDLLPath, @"profile.png");
                }
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.GET.profileicon.failed", $"{Loc.GetString("GETProfileIconFailed")} - {ex.Message}", GravitonSeverity.Error));
            }
            
            _plugin.Settings.UserType = userinfo.Role;
            _plugin.Settings.User = userinfo.Username;
            _plugin.Settings.UserID = userinfo.Id;
            return true;

        }

        async Task<bool> RegisterNewDevice()
        {
            // Check to see if current device id is valid
            if (!string.IsNullOrEmpty(_plugin.Settings.DeviceID))
            {
                var result = await HttpClientSingleton.RomMGetAsync("/api/devices");
                if (result != null)
                {
                    try
                    {
                        List<RomMDevice> devices = result.RootElement.Deserialize<List<RomMDevice>>() ?? throw new Exception("Unable to deserialize UserInfo!");
                        if (devices.Any(x => x.ID == _plugin.Settings.DeviceID))
                            return true;
                    }
                    catch (Exception ex)
                    {
                        GravitonNotify.Add(new GravitonNotification("graviton.GET.device.failed", $"{Loc.GetString("GETDevicesFailed")} - {ex.Message}", GravitonSeverity.Warn));
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
            newDevice.HostName = Environment.MachineName;

            var request = await HttpClientSingleton.RomMPostJsonAsync("/api/devices", newDevice);
            if (request == null)
                return false;

            try
            {
                RomMRegisterDeviceResponse newRomMDevice = request.RootElement.Deserialize<RomMRegisterDeviceResponse>() ?? throw new Exception("Unable to deserialize register device response!");

                // Set ID that RomM responds with
                _plugin.Settings.DeviceID = newRomMDevice.DeviceID ?? throw new Exception("Response Device ID is null!");
                return true;
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.POST.device.failed", $"{Loc.GetString("CreateNewDeviceFailed")} - {ex.Message}", GravitonSeverity.Error));
                return false;
            }
        }

        async Task<bool> UpdateDevice()
        {
            if (string.IsNullOrEmpty(_plugin.Settings.DeviceID))
                return false;

            // Rebuild device data
            RomMRegisterDevice newDevice = new();
            newDevice.Platform = "Windows";
            newDevice.Client = "Graviton (Playnite Plugin)";
            newDevice.ClientVersion = GravitonPlugin.Version.ToString();
            newDevice.MACAddress = (from nic in NetworkInterface.GetAllNetworkInterfaces() where nic.OperationalStatus == OperationalStatus.Up select nic.GetPhysicalAddress().ToString()).FirstOrDefault();
            newDevice.HostName = Environment.MachineName;

            var result = await HttpClientSingleton.RomMPutJsonAsync($"/api/devices/{_plugin.Settings.DeviceID}", newDevice);
            if (result == null)
                return false;

            return true;
        }

        void SyncFailed()
        {
            _plugin.Settings.ProfilePath = Path.Combine(_plugin.PluginDLLPath, @"profile.png");
            _plugin.Settings.User = "----";
            _plugin.Settings.UserType = "----";
            _plugin.Settings.ServerVersion = "---";
            _plugin.Settings.LastAuthenticated = null;
        }
    }
}
