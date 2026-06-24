using Graviton.Models.Notifications;
using Graviton.Models.RomM;
using Graviton.Models.RomM.Collection;

using Playnite;
using Playnite.WebViews;

using QRCoder;
using QRCoder.Xaml;

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class AuthenticationTab : UserControl
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        Dictionary<string, string[]> ImageFileChoices = new Dictionary<string, string[]>()
        {
            {"Image File", ["*.png","*.jpg", "*.jpeg","*.webp"]}
        };

        public AuthenticationTab()
        {
            InitializeComponent();

            AuthButtonText.Text = Loc.GetString("AuthButton");
            ServerHostText.Text = Loc.GetString("ServerText");
            TokenText.Text = Loc.GetString("ClientToken");
            UseBasicAuthText.Text = Loc.GetString("UseBasicAuth");
            UserPassWarning.Text = Loc.GetString("UserPassWarning");
            UsernameText.Text = Loc.GetString("Username");
            PasswordText.Text = Loc.GetString("Password");

            ProfileEditButton.FontFamily = Playnite.Fonts.NerdFont;
            ShowPassword.FontFamily = Playnite.Fonts.NerdFont;

            RomMPassword.Password = _plugin.Settings.PasswordNP;
            RomMPassword.PasswordChanged += (_, _) =>
            {
                _plugin.Settings.PasswordNP = RomMPassword.Password;
            };

            RomMPassword.Background = RomMUsername.Background;
            RomMPassword.BorderBrush = RomMUsername.BorderBrush;

        }

        private async void Click_Authenticate(object sender, System.Windows.RoutedEventArgs e)
        {
            TestServer.IsEnabled = false;
            await _plugin.Account!.Login();
            TestServer.IsEnabled = true;

            e.Handled = true;
        }

        private async void Click_UpdateProfileIcon(object sender, RoutedEventArgs e)
        {

            var image = await _playniteAPI.Dialogs.SelectFileAsync(ImageFileChoices);
            if (image != null && image.Count == 1)
            {
                var fileBytes = File.ReadAllBytes(image[0]);
                var fileName = Path.GetFileName(image[0]);

                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                string filetype;

                switch (ext)
                {
                    case ".jpg":
                        filetype = "image/jpeg";
                        break;

                    case ".jpeg":
                        filetype = "image/jpeg";
                        break;

                    case ".png":
                        filetype = "image/png";
                        break;

                    case ".webp":
                        filetype = "image/webp";
                        break;

                    default:
                        filetype = "application/octet-stream";
                        break;
                }

                var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(filetype);

                content.Add(fileContent, "avatar", fileName);
                var result = await HttpClientSingleton.RomMPutContentAsync($"/api/users/{_plugin.Settings.UserID}", content);

                if(result != null)
                {
                    File.WriteAllBytes($"{_playniteAPI.UserDataDir}\\avatar.png", fileBytes);
                    _plugin.Settings.ProfilePath = $"{_playniteAPI.UserDataDir}\\avatar.png";
                }
                else
                {
                    _plugin.Settings.ProfilePath = $"{_plugin.PluginDLLPath}\\profile.png";
                }
            }

            e.Handled = true;   
        }

        private void Hyperlink_ClientToken(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {

            if(string.IsNullOrEmpty(_plugin.Settings.Host))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.openuri.clienttoken.failed", Loc.GetString("ClientTokenAddressFailed"), GravitonSeverity.Error));
                e.Handled= true;
                return;
            }

            string clienttokenaddress = _plugin.Settings.Host + "/client-api-tokens";
            Process.Start(new ProcessStartInfo(clienttokenaddress) { UseShellExecute = true })?.Dispose();
            e.Handled = true;
        }

        private void Unchecked_UseBasicAuth(object sender, RoutedEventArgs e)
        {
            RomMPasswordUnmasked.Visibility = Visibility.Collapsed;
            RomMPassword.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private void Checked_UseBasicAuth(object sender, RoutedEventArgs e)
        {
            RomMPassword.Visibility = Visibility.Visible;
            RomMPasswordUnmasked.Visibility = Visibility.Collapsed;
            ShowPassword.Content = "\uea70";
            e.Handled = true;
        }

        private void Click_ShowPassword(object sender, RoutedEventArgs e)
        {
            if(RomMPassword.Visibility == Visibility.Visible)
            {
                RomMPassword.Visibility = Visibility.Collapsed;
                RomMPasswordUnmasked.Visibility = Visibility.Visible;
                ShowPassword.Content = "\ueae7";
            }
            else
            {
                RomMPassword.Visibility = Visibility.Visible;
                RomMPasswordUnmasked.Visibility = Visibility.Collapsed;
                ShowPassword.Content = "\uea70";
                RomMPassword.Password = _plugin.Settings.PasswordNP;
            }

            e.Handled = true;
        }

        private async void Click_LoginViaQR(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_plugin.Settings.Host))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.login.host.notset", Loc.GetString("HostNotSet"), GravitonSeverity.Warn));
                e.Handled = true;
                return;
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
                    e.Handled = true;
                    return;
                }
                var result = JsonSerializer.Deserialize<RomMPairDevice>(response);
                if (result == null)
                {
                    e.Handled = true;
                    return;
                }

                _ = Task.Run(async () => UpdateQR(result));
                QRAuth.IsEnabled = false;
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.login.QR.failed", $"Failed to setup QR code - {ex.Message}", GravitonSeverity.Error, ex));
                e.Handled = true;
                return;
            }

            e.Handled = true;
        }


        private async void UpdateQR(RomMPairDevice pairDevice)
        {
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode($"{_plugin.Settings.Host}{pairDevice.VeificationPathComplete}", QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new XamlQRCode(qrCodeData))
            {
                DrawingImage qrImage = qrCode.GetGraphic(20);
                qrImage.Freeze();
                UIDispatcher.Invoke(() => LoginQR.Source = qrImage);
            }

            var intervalMillisecs = pairDevice.Interval * 1000;
            var deviceCode = new { device_code = pairDevice.DeviceCode };      
            HttpStatusCode status = HttpStatusCode.OK;

            var startTime = DateTime.UtcNow;
            var expiresin = TimeSpan.FromSeconds(pairDevice.ExpiresIn - 1);
            while ((DateTime.UtcNow - startTime) < expiresin)
            {
                if(intervalMillisecs <= 0)
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
                            GravitonNotify.Add(new GravitonNotification("graviton.pair.device.failed", $"Failed to pair with server - Response was null", GravitonSeverity.Error));
                            break;
                        }
                           
                        _plugin.Settings.DeviceID = result.DeviceID!;
                        _plugin.Settings.ClientTokenNP = result.AccessToken!;
                        await _plugin.Account?.Login()!;
                        
                        break;

                    }
                    catch (Exception ex)
                    {
                        if(response != null)
                        {
                            var result = await response.Content.ReadAsStringAsync();
                            if(result.Contains("expired_token"))
                            {
                                GravitonNotify.Add(new GravitonNotification("graviton.pair.device.failed", $"Failed to pair with server - Expired", GravitonSeverity.Error));
                                break;
                            }
                            if (result.Contains("access_denied"))
                            {
                                GravitonNotify.Add(new GravitonNotification("graviton.pair.device.failed", $"Failed to pair with server - Request was denied", GravitonSeverity.Error));
                                break;
                            }

                            if(response.StatusCode != HttpStatusCode.BadRequest)
                            {
                                GravitonNotify.Add(new GravitonNotification("graviton.pair.device.failed", $"Failed to pair with server - {ex.Message}", GravitonSeverity.Error, ex));
                                break;
                            }
                        }
                    }

                    intervalMillisecs = pairDevice.Interval * 1000;
                }

                UIDispatcher.Invoke(() => LoginQRTimer.Text = $"Expires in: {(((expiresin - (DateTime.UtcNow - startTime)).TotalMilliseconds) / 1000 ).ToString("F1")}s");

                await Task.Delay(100);
                intervalMillisecs -= 100;
            }

            UIDispatcher.Invoke(() => LoginQR.Source = null);
            UIDispatcher.Invoke(() => LoginQRTimer.Text = "");
            UIDispatcher.Invoke(() => QRAuth.IsEnabled = true);
        }
    }
}
