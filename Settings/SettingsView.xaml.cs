using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Playnite.SDK;

using QRCoder;
using QRCoder.Xaml;

using RomM.Models.RomM;
using RomM.Models.RomM.Platform;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RomM.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void Click_TestConnection(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.TestConnection(true);
            e.Handled = true;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                if (e.Uri.Scheme == Uri.UriSchemeHttp || e.Uri.Scheme == Uri.UriSchemeHttps)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = e.Uri.AbsoluteUri,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
            }
            e.Handled = true;
        }

        private async void Click_PullPlatforms(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;

            try
            {
                HttpResponseMessage response = await HttpClientSingleton.Instance.GetAsync($"{SettingsViewModel.Instance.RomMHost}/api/platforms");
                response.EnsureSuccessStatusCode();

                string body = await response.Content.ReadAsStringAsync();
                SettingsViewModel.Instance.RomMPlatforms = JsonConvert.DeserializeObject<List<RomMPlatform>>(body);
                SettingsViewModel.Instance.UpdateNotificationBar("Platforms successfully retrieved!");
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error($"RomM - failed to get platforms: {ex}");
                SettingsViewModel.Instance.UpdateNotificationBar($"Failed to get platforms: {ex.Message}!", true);
            }
        }

        private void Click_AddMapping(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Mappings.Add(new EmulatorMapping(SettingsViewModel.Instance.RomMPlatforms));
        }

        private void Click_Delete(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is EmulatorMapping mapping)
            {
                var res = SettingsViewModel.Instance.PlayniteAPI.Dialogs.ShowMessage(string.Format("Delete this mapping?\r\n\r\n{0}", mapping.GetDescriptionLines().Aggregate((a, b) => $"{a}{Environment.NewLine}{b}")), "Confirm delete", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                {
                    SettingsViewModel.Instance.Mappings.Remove(mapping);
                }
            }
        }

        private void Click_BrowseDestination(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            string path;
            if ((path = GetSelectedFolderPath()) == null) return;
            var playnite = SettingsViewModel.Instance.PlayniteAPI;
            if (playnite.Paths.IsPortable)
            {
                path = path.Replace(playnite.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory);
            }

            mapping.DestinationPath = path;
        }

        private static string GetSelectedFolderPath()
        {
            return SettingsViewModel.Instance.PlayniteAPI.Dialogs.SelectFolder();
        }

        private void Click_Browse7zDestination(object sender, RoutedEventArgs e)
        {
            string path;
            if ((path = SettingsViewModel.Instance.PlayniteAPI.Dialogs.SelectFile("7Zip Executable|7z.exe")) == null) return;

            SettingsViewModel.Instance.PathTo7z = path;
            e.Handled = true;
        }

        private async void Click_LoginViaQR(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SettingsViewModel.Instance.RomMHost))
            {
                SettingsViewModel.Instance.UpdateNotificationBar("RomMHost is empty!", true);
                e.Handled = true;
                return;
            }

            try
            {
                HttpResponseMessage response = await HttpClientSingleton.Instance.GetAsync($"{SettingsViewModel.Instance.RomMHost}/api/heartbeat");
                response.EnsureSuccessStatusCode();

                Stream body = await response.Content.ReadAsStreamAsync();
                using (StreamReader reader = new StreamReader(body))
                {
                    var jsonResponse = JObject.Parse(reader.ReadToEnd());
                    var heartbeat = jsonResponse.ToObject<RomMHeartbeat>();

                    string raw = (heartbeat.SystemInfo.Version ?? string.Empty).Split('-', '+')[0];
                    if (Version.TryParse(raw, out Version parsed))
                        if(parsed.CompareTo(new Version(5,0,0)) < 0)
                        {
                            SettingsViewModel.Instance.UpdateNotificationBar("Server needs to be v5.0 or later!", true);
                            e.Handled = true;
                            return;
                        }
                }

                var deviceInit = new
                {
                    client_device_identifier = $"Playnite-{Environment.MachineName}",
                    name = $"Playnite-{Environment.MachineName}",
                    client = "Playnite Plugin",
                    platform = "Windows",
                    client_version = "0.7.0", // This should be probably be changed to reading the extension.yaml at runtime or
                                              //    adding a plugin version to the main file that devs change every update
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

                var initContent = new StringContent(JsonConvert.SerializeObject(deviceInit), Encoding.UTF8, "application/json");
                response = await HttpClientSingleton.Instance.PostAsync($"{SettingsViewModel.Instance.RomMHost}/api/auth/device/init", initContent);
                response.EnsureSuccessStatusCode();

                body = await response.Content.ReadAsStreamAsync();
                using (StreamReader reader = new StreamReader(body))
                {
                    var jsonResponse = JObject.Parse(reader.ReadToEnd());
                    var pairDevice = jsonResponse.ToObject<RomMPairDevice>();
                    QRAuth.IsEnabled = false;
                    await UpdateQR(pairDevice);
                }
     
            }
            catch (Exception ex)
            {
                SettingsViewModel.Instance.UpdateNotificationBar($"Failed to login via QR! - {ex.Message}", true);
                LogManager.GetLogger().Error($"Failed to login via QR! - {ex}");
                e.Handled = true;
                return;
            }

            e.Handled = true;
        }

        private async Task UpdateQR(RomMPairDevice pairDevice)
        {
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode($"{SettingsViewModel.Instance.RomMHost}{pairDevice.VerificationPathComplete}", QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new XamlQRCode(qrCodeData))
            {
                DrawingImage qrImage = qrCode.GetGraphic(20);
                qrImage.Freeze();
                LoginQR.Source = qrImage;
            }

            var intervalMillisecs = pairDevice.Interval * 1000;
            var deviceCode = new { device_code = pairDevice.DeviceCode };
            
            var startTime = DateTime.UtcNow;
            var expiresin = TimeSpan.FromSeconds(pairDevice.ExpiresIn - 1);
            while ((DateTime.UtcNow - startTime) < expiresin)
            {
                if (intervalMillisecs <= 0)
                {
                    HttpResponseMessage response = null;
                    string result = ""; 
                    try
                    {
                        var deviceCodeContent = new StringContent(JsonConvert.SerializeObject(deviceCode), Encoding.UTF8, "application/json");
                        response = await HttpClientSingleton.Instance.PostAsync($"{SettingsViewModel.Instance.RomMHost}/api/auth/device/token", deviceCodeContent);
                        result = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();

                        var pairResponse = JsonConvert.DeserializeObject<RomMPairDeviceResponse>(result);
                        SettingsViewModel.Instance.RomMDeviceID = pairResponse.DeviceID;
                        SettingsViewModel.Instance.RomMClientToken = pairResponse.AccessToken;
                        SettingsViewModel.Instance.TestConnection(true, true);
                        break;

                    }
                    catch (Exception ex)
                    {
                        if (response == null)
                        {
                            SettingsViewModel.Instance.UpdateNotificationBar($"Failed to login via QR! - No Response", true);
                            LogManager.GetLogger().Error($"Failed to login via QR! - No Response - {ex}");
                            break;
                        }

                        if (result.Contains("expired_token"))
                        {
                            SettingsViewModel.Instance.UpdateNotificationBar($"Failed to login via QR! - Login Expired", true);
                            LogManager.GetLogger().Error($"Failed to login via QR! - Login Expired");
                            break;
                        }
                        if (result.Contains("access_denied"))
                        {
                            SettingsViewModel.Instance.UpdateNotificationBar($"Failed to login via QR! - Access Denied", true);
                            LogManager.GetLogger().Error($"Failed to login via QR! - Access Denied");
                            break;
                        }

                        if (response.StatusCode != HttpStatusCode.BadRequest)
                        {
                            SettingsViewModel.Instance.UpdateNotificationBar($"Failed to login via QR! - {ex.Message}", true);
                            LogManager.GetLogger().Error($"Failed to login via QR! - {ex}");
                            break;
                        }
                    }

                    intervalMillisecs = pairDevice.Interval * 1000;
                }

                LoginQRTimer.Text = $"Expires in: {(((expiresin - (DateTime.UtcNow - startTime)).TotalMilliseconds) / 1000).ToString("F1")}s";

                await Task.Delay(100);
                intervalMillisecs -= 100;
            }

            LoginQR.Source = null;
            LoginQRTimer.Text = "";
            QRAuth.IsEnabled = true;
        }
    }
}