using Graviton.Models.Notifications;
using Graviton.Models.RomM;

using Playnite;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace Graviton.Settings
{
    internal class RomMAccount
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        private static readonly Regex _iconPathRegex = new Regex(@"^users/[^/]+/profile/avatar\.(png|jpg|jpeg|webp)$");

        public RomMAccount(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
        }

        public async Task<ServerInfo?> Heartbeat()
        {
            var result = await HttpClientSingleton.RomMGetAsync("/api/heartbeat", true);
            if(result == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.heartbeat.failed", Loc.GetString("HeartbeatFailed"), GravitonSeverity.Error));
                return null;
            }

            try
            {         
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

            _plugin.Settings.AccountState.LastAuthenticated = DateTime.UtcNow;

            ServerInfo? heartbeat = await Heartbeat();
            if (heartbeat == null)
            {
                SyncFailed(); 
                return false;
            }
                
            _plugin.Settings.AccountState.ServerVersion = heartbeat.Value.Version;


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

            if (!(await SyncPlatforms()))
            {
                SyncFailed();
                return false;
            }

            return true;
        }

        public async Task<bool> SyncPlatforms()
        {
            foreach (var mapping in _plugin.Settings.Mappings!)
            {
                mapping.AvailablePlatforms = _plugin.Settings.AccountState.RomMPlatforms;
            }

            var importcontroller = _plugin?.ImportController;

            if (importcontroller == null)
            {
                return false;
            }

            var platforms = await importcontroller.FetchPlatforms();
            if (platforms == null)
                return false;
            else if (platforms.Count <= 0)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.GET.no.platforms", Loc.GetString("NoPlatforms"), GravitonSeverity.Warn));
                return false;
            }

            _plugin?.Settings.AccountState.RomMPlatforms = platforms.ToObservableCollection();
            foreach (var mapping in _plugin?.Settings.Mappings!)
            {
                mapping.AvailablePlatforms = platforms.ToObservableCollection();
            }

            return true;
        }

        public async Task<bool> SyncUserData()
        {
            var result = await HttpClientSingleton.RomMGetAsync("/api/users/me");
            if (result == null)
            {
                SyncFailed();
                return false;
            }
                  
            try
            {
                var userinfo = result.RootElement.Deserialize<RomMUser>() ?? throw new Exception("Failed to deserialize UserInfo!");

                _plugin.Settings.AccountState.LastAuthenticated = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(userinfo.IconPath) && _iconPathRegex.IsMatch(userinfo.IconPath))
                {
                    var response = await HttpClientSingleton.Instance.GetAsync($"{_plugin.Settings.Host}/api/users/{userinfo.Id}/avatar", System.Net.Http.HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken());
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

                _plugin.Settings.AccountState.UserType = userinfo.Role;
                _plugin.Settings.AccountState.User = userinfo.Username;
                _plugin.Settings.AccountState.UserID = userinfo.Id;
                return true;

            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.GET.profileicon.failed", Loc.GetString("GETProfileIconFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                _plugin.Settings.ProfilePath = Path.Combine(_plugin.PluginDLLPath, @"profile.png");
            }

            return false;
        }

        async Task<bool> RegisterNewDevice()
        {
            // Check to see if current device id is valid
            if (!string.IsNullOrEmpty(_plugin.Settings.AccountState.DeviceID))
            {
                var result = await HttpClientSingleton.RomMGetAsync("/api/devices");
                if (result != null)
                {
                    try
                    {
                        List<RomMDevice> devices = result.RootElement.Deserialize<List<RomMDevice>>() ?? throw new Exception("Unable to deserialize UserInfo!");
                        if (devices.Any(x => x.ID == _plugin.Settings.AccountState.DeviceID))
                            return true;
                    }
                    catch (Exception ex)
                    {
                        GravitonNotify.Add(new GravitonNotification("graviton.GET.device.failed", Loc.GetString("GETDevicesFailed", ("Error", ex.Message)), GravitonSeverity.Warn, ex));
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
                _plugin.Settings.AccountState.DeviceID = newRomMDevice.DeviceID ?? throw new Exception("Response Device ID is null!");
                return true;
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.POST.device.failed", Loc.GetString("CreateNewDeviceFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                return false;
            }
        }

        async Task<bool> UpdateDevice()
        {
            if (string.IsNullOrEmpty(_plugin.Settings.AccountState.DeviceID))
                return false;

            // Rebuild device data
            RomMRegisterDevice newDevice = new();
            newDevice.Platform = "Windows";
            newDevice.Client = "Graviton (Playnite Plugin)";
            newDevice.ClientVersion = GravitonPlugin.Version.ToString();
            newDevice.MACAddress = (from nic in NetworkInterface.GetAllNetworkInterfaces() where nic.OperationalStatus == OperationalStatus.Up select nic.GetPhysicalAddress().ToString()).FirstOrDefault();
            newDevice.HostName = Environment.MachineName;

            var result = await HttpClientSingleton.RomMPutJsonAsync($"/api/devices/{_plugin.Settings.AccountState.DeviceID}", newDevice);
            if (result == null)
                return false;

            return true;
        }

        public async Task<RomMPairDevice?> InitDevicePair()
        {
            if (string.IsNullOrEmpty(_plugin.Settings.Host))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.login.host.notset", Loc.GetString("HostNotSet"), GravitonSeverity.Warn));
                return null;
            }

            var deviceInit = new
            {
                client_device_identifier = $"Graviton-{Environment.MachineName}",
                name = $"Graviton-{Environment.MachineName}",
                client = "Graviton (Playnite Plugin)",
                platform = "Windows",
                client_version = GravitonPlugin.Version,
                requested_scopes = new List<string>
                {
                    "me.read", "me.write",
                    "assets.read", "assets.write",
                    "devices.read", "devices.write",
                    "roms.user.read","roms.user.write",
                    "roms.read",
                    "platforms.read",
                    "firmware.read",
                    "collections.read", "collections.write"
                }
            };

            try
            {
                var response = await HttpClientSingleton.RomMPostJsonAsync("/api/auth/device/init", deviceInit);
                if (response == null)
                {
                    return null;
                }
                var result = JsonSerializer.Deserialize<RomMPairDevice>(response);
                if (result == null)
                {
                    return null;
                }

                return result;

            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.login.QR.failed", Loc.GetString("FailedQRSetup", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                return null;
            }
        }

        public async Task<bool> StartDevicePair(RomMPairDevice pairDevice, TextBlock LoginQRTimer)
        {
            var intervalMillisecs = pairDevice.Interval * 1000;
            var deviceCode = new { device_code = pairDevice.DeviceCode };
            HttpStatusCode status = HttpStatusCode.OK;

            var startTime = DateTime.UtcNow;
            var expiresin = TimeSpan.FromSeconds(pairDevice.ExpiresIn - 1);
            while ((DateTime.UtcNow - startTime) < expiresin)
            {
                if (intervalMillisecs <= 0)
                {
                    HttpResponseMessage? response = null;

                    try
                    {
                        response = await HttpClientSingleton.Instance.PostAsJsonAsync($"{_plugin.Settings.Host}/api/auth/device/token", deviceCode);
                        status = response.StatusCode;
                        response.EnsureSuccessStatusCode();

                        var stream = await response.Content.ReadAsStreamAsync();
                        var json = await JsonDocument.ParseAsync(stream);
                        var result = JsonSerializer.Deserialize<RomMPairDeviceResponse>(json);

                        if (result == null)
                        {
                            GravitonNotify.Add(new GravitonNotification("graviton.pair.device.failed", Loc.GetString("FailedServerPair", ("Error", Loc.GetString("PairWasNull"))), GravitonSeverity.Error));
                            return false;
                        }

                        _plugin.Settings.AccountState.DeviceID = result.DeviceID!;
                        _plugin.Settings.ClientTokenNP = result.AccessToken!;
                        await _plugin.Account?.Login()!;
                        return true;

                    }
                    catch (Exception ex)
                    {
                        if (response != null)
                        {
                            var result = await response.Content.ReadAsStringAsync();
                            if (result.Contains("expired_token"))
                            {
                                GravitonNotify.Add(new GravitonNotification("graviton.pair.device.failed", Loc.GetString("FailedServerPair", ("Error", Loc.GetString("PairExpired"))), GravitonSeverity.Info));
                                return false;
                            }
                            if (result.Contains("access_denied"))
                            {
                                GravitonNotify.Add(new GravitonNotification("graviton.pair.device.failed", Loc.GetString("FailedServerPair", ("Error", Loc.GetString("PairWasDenied"))), GravitonSeverity.Warn));
                                return false;
                            }

                            if (response.StatusCode != HttpStatusCode.BadRequest)
                            {
                                GravitonNotify.Add(new GravitonNotification("graviton.pair.device.failed", Loc.GetString("FailedServerPair", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                                return false;
                            }
                        }
                    }

                    intervalMillisecs = pairDevice.Interval * 1000;
                }

                UIDispatcher.Invoke(() => LoginQRTimer.Text = $"Expires in: {(((expiresin - (DateTime.UtcNow - startTime)).TotalMilliseconds) / 1000).ToString("F1")}s");

                await Task.Delay(100);
                intervalMillisecs -= 100;
            }

            GravitonNotify.Add(new GravitonNotification("graviton.pair.device.failed", Loc.GetString("FailedServerPair", ("Error", Loc.GetString("PairExpired"))), GravitonSeverity.Info));
            return false;
        }

        void SyncFailed()
        {
            _plugin.Settings.ProfilePath = Path.Combine(_plugin.PluginDLLPath, @"profile.png");
            _plugin.Settings.AccountState.User = "----";
            _plugin.Settings.AccountState.UserType = "----";
            _plugin.Settings.AccountState.ServerVersion = "---";
            _plugin.Settings.AccountState.LastAuthenticated = null;
        }
    }
}
